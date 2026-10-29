using Client.PlaytestReceiver.Delivery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload.Attempt;

namespace Client.PlaytestReceiver.Upload
{
    // outbox走査と印名の正本。箱の状態は BoxState、送る中身はここが決める
    // Owns the outbox scan and marker names; box state is PlaytestOutboxBoxState's, what ships is decided only here
    public static class PlaytestOutboxScanner
    {
        // READYはバグ報告の書き出し側（BugReportOutbox）が打つ印。別アセンブリなので名前の一致はテストで固定する
        // READY is written by the bug report side (BugReportOutbox); it lives in another assembly, so a test pins the match
        public const string ReadyMarker = "READY";
        public const string UploadedMarker = "UPLOADED";
        public const string FailedMarker = "UPLOAD_FAILED";
        public const string AttemptsMarker = "UPLOAD_ATTEMPTS";
        public const string SkippedMarker = "UPLOAD_SKIPPED";
        public const string CompletionHoldPrefix = "evidence-completion-required: ";
        public const string LockMarker = "UPLOAD_LOCK";
        public const string DeclaredMarker = "UPLOAD_DECLARED";
        public const string ShippedMarker = "SHIPPED";

        private static readonly string[] Markers = { ReadyMarker, UploadedMarker, FailedMarker, AttemptsMarker, SkippedMarker, DeclaredMarker, LockMarker, ShippedMarker };

        public static IReadOnlyList<PlaytestOutboxBox> ScanPending(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope)
        {
            return Scan(directories, scope, false);
        }

        public static IReadOnlyList<PlaytestOutboxBox> ScanForEvidence(PlaytestOutboxDirectories directories)
        {
            return Scan(directories, PlaytestOutboxScope.ReportsOnly, true);
        }

        public static bool IsCompletionHold(string directory)
        {
            if (File.Exists(Path.Combine(directory, DeclaredMarker))) return false; // silent-ok: 宣言済み本文は不変 / 保留を画面に保持
            var path = Path.Combine(directory, FailedMarker);
            if (!File.Exists(path)) return false; // silent-ok: 保留印なし / 通常走査で配送する
            var reason = File.ReadAllText(path);
            if (reason.StartsWith(CompletionHoldPrefix, StringComparison.Ordinal) ||
                reason == "repo/commit-evidence: Editorで元SHAの資料補完が必要です") return true;
            // 旧Editor理由は未補完の実体と照合
            // Match legacy Editor reasons against evidence awaiting completion
            var legacy = reason == "元repoまたはmanifestのSHAを特定できないため送信を保留しました" ||
                         System.Text.RegularExpressions.Regex.IsMatch(reason, @"\A元SHA [0-9a-fA-F]{40} を指定repoで確認できません: ");
            if (!legacy) return false; // silent-ok: 他の恒久失敗は維持 / 保留印と画面に残る
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
            if (manifest["isEditor"]?.Type != JTokenType.Boolean || !(bool)manifest["isEditor"]) return false; // silent-ok: Editor箱だけ補完 / 保留印を維持
            var statusPath = Path.Combine(directory, "repo/bundle-status.txt");
            var statuses = File.Exists(statusPath) ? File.ReadAllLines(statusPath) : Array.Empty<string>();
            return !new[] { "commits.bundle", "master-commits.bundle" }.All(name =>
                statuses.Any(line => line.StartsWith(name + ": ", StringComparison.Ordinal) && !line.Contains(": failed-delivery")));
        }

        private static IReadOnlyList<PlaytestOutboxBox> Scan(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope, bool includeCompletionHolds)
        {
            var boxes = new List<PlaytestOutboxBox>();
            boxes.AddRange(ScanOne(directories.ReportOutbox, PlaytestUploadKind.Report, directories.ReportFilePolicy));
            if (scope.IncludesProgress()) boxes.AddRange(ScanOne(directories.ProgressOutbox, PlaytestUploadKind.Progress, directories.ProgressFilePolicy));

            // 箱のIDは yyyyMMdd_HHmmss_<hex> なので辞書順が時刻順になる。古い順に送る
            // Bundle ids are yyyyMMdd_HHmmss_<hex>, so lexicographic order is chronological; ship oldest first
            return boxes.OrderBy(box => box.BundleId, StringComparer.Ordinal).ToList();

            #region Internal

            IEnumerable<PlaytestOutboxBox> ScanOne(string outbox, PlaytestUploadKind kind, IPlaytestBoxFilePolicy filePolicy)
            {
                foreach (var directory in EnumerateBoxDirectories(outbox))
                {
                    PlaytestOutboxBoxKind boxKind;
                    // 外部ディスク境界。読めない箱を隔離し、件数不明は在庫表示へ残す
                    // Isolate unreadable boxes at the disk boundary; inventory still exposes the unknown count
                    try { boxKind = PlaytestOutboxBoxState.Classify(directory); }
                    catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                    {
                        UnityEngine.Debug.LogWarning($"[PlaytestReceiver] cannot scan box {directory}: {error.Message}");
                        continue;
                    }
                    if (boxKind == PlaytestOutboxBoxKind.NotReady || boxKind == PlaytestOutboxBoxKind.Delivered) continue;
                    if (boxKind == PlaytestOutboxBoxKind.Failed && !CanResume(directory, filePolicy, includeCompletionHolds)) continue;
                    yield return new PlaytestOutboxBox(directory, Path.GetFileName(directory), kind, filePolicy);
                }
            }

            #endregion
        }

        // 容量保留か補完保留か。読めなければ例外を投げ、呼び手が箱単位で隔離する
        // Capacity or completion hold; throws when unreadable so each caller isolates it per box
        internal static bool IsHold(string directory, IPlaytestBoxFilePolicy filePolicy, bool includeCompletionHolds)
        {
            return filePolicy.CanResumeHold(directory) || (includeCompletionHolds && IsCompletionHold(directory));
        }

        private static bool CanResume(string directory, IPlaytestBoxFilePolicy filePolicy, bool includeCompletionHolds)
        {
            // 外部ディスク・JSON境界。読めない箱だけ保留し、印を画面へ残す
            // Isolate external disk and JSON failures to this box; preserve its visible failure marker
            try
            {
                return IsHold(directory, filePolicy, includeCompletionHolds);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
            {
                UnityEngine.Debug.LogWarning($"[PlaytestReceiver] cannot classify held box {directory}: {error.Message}");
                return false;
            }
        }

        internal static PlaytestOutboxInventoryRead ReadDeliveryInventory(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope)
        {
            var pending = 0;
            var failed = 0;
            // 外部ディスク境界。読取不能は件数不明として画面とログへ渡す
            // External disk boundary: expose unreadable inventory to the UI and log without invented counts
            try
            {
                Count(directories.ReportOutbox);
                if (scope.IncludesProgress()) Count(directories.ProgressOutbox);
                return new PlaytestOutboxDeliveryCounts(pending, failed);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                UnityEngine.Debug.LogWarning($"[PlaytestReceiver] outbox inventory unavailable: {exception.Message}");
                return new PlaytestOutboxInventoryUnavailable(exception.Message);
            }

            #region Internal

            void Count(string outbox)
            {
                // 存在しないoutboxはまだ報告を作っていない状態
                // An absent outbox means no report has been created yet
                foreach (var directory in EnumerateBoxDirectories(outbox))
                {
                    switch (PlaytestOutboxBoxState.Classify(directory))
                    {
                        case PlaytestOutboxBoxKind.Failed: failed++; break;
                        case PlaytestOutboxBoxKind.Pending: pending++; break;
                    }
                }
            }

            #endregion
        }

        public static IReadOnlyList<string> EnumerateBoxDirectories(string outbox)
        {
            // 外部ディスク境界。未作成だけを空扱いし、権限エラーは呼び手へ伝える
            // External disk boundary: only an absent root is empty; access failures reach the caller
            try { return Directory.GetDirectories(outbox); }
            catch (DirectoryNotFoundException)
            {
                // silent-ok: 未作成のoutboxには箱がない / 作成後の次回要求で再走査する
                return Array.Empty<string>();
            }
        }

        public static IReadOnlyList<string> ListPayloadFiles(string boxDirectory)
        {
            // 印は箱の直下にしか置かれない。名前だけで判定すると logs/READY のような中身まで落ちる
            // Markers live only at the box root; matching on the name alone would also drop payloads such as logs/READY
            return Directory.GetFiles(boxDirectory, "*", SearchOption.AllDirectories)
                .Where(path => !Markers.Contains(ToRelativePath(boxDirectory, path)))
                .ToList();
        }

        public static string ToRelativePath(string boxDirectory, string filePath)
        {
            var relative = filePath.Substring(boxDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
