using Client.PlaytestReceiver.Delivery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload.Attempt;

namespace Client.PlaytestReceiver.Upload
{
    // outboxの走査と印の名前の正本。どの箱が未送で、箱の中の何を送るのかはここだけが決める
    // Owns the outbox scan and the marker names; which boxes are pending and what inside them ships is decided only here
    public static class PlaytestOutboxScanner
    {
        // READYはバグ報告の書き出し側（BugReportOutbox）が打つ印。別アセンブリなので名前の一致はテストで固定する
        // READY is written by the bug report side (BugReportOutbox); it lives in another assembly, so a test pins the match
        public const string ReadyMarker = "READY";
        public const string UploadedMarker = "UPLOADED";
        public const string FailedMarker = "UPLOAD_FAILED";
        public const string AttemptsMarker = "UPLOAD_ATTEMPTS";
        public const string SkippedMarker = "UPLOAD_SKIPPED";
        public const string DeclaredMarker = "UPLOAD_DECLARED";

        private static readonly string[] Markers = { ReadyMarker, UploadedMarker, FailedMarker, AttemptsMarker, SkippedMarker, DeclaredMarker, "SHIPPED" };

        public static IReadOnlyList<PlaytestOutboxBox> ScanPending(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope)
        {
            var boxes = new List<PlaytestOutboxBox>();
            boxes.AddRange(ScanOne(directories.ReportOutbox, PlaytestUploadKind.Report, directories.ReportFilePolicy));
            if (scope == PlaytestOutboxScope.ReportsAndProgress) boxes.AddRange(ScanOne(directories.ProgressOutbox, PlaytestUploadKind.Progress, directories.ProgressFilePolicy));

            // 箱のIDは yyyyMMdd_HHmmss_<hex> なので辞書順が時刻順になる。古い順に送る
            // Bundle ids are yyyyMMdd_HHmmss_<hex>, so lexicographic order is chronological; ship oldest first
            return boxes.OrderBy(box => box.BundleId, StringComparer.Ordinal).ToList();

            #region Internal

            IEnumerable<PlaytestOutboxBox> ScanOne(string outbox, PlaytestUploadKind kind, IPlaytestBoxFilePolicy filePolicy)
            {
                foreach (var directory in EnumerateOutboxDirectories(outbox))
                {
                    var markers = ReadMarkers(directory);
                    if (!markers.Contains(ReadyMarker)) continue;
                    if (IsDelivered(markers)) continue;
                    if (markers.Contains(FailedMarker)) continue;
                    yield return new PlaytestOutboxBox(directory, Path.GetFileName(directory), kind, filePolicy);
                }
            }

            #endregion
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
                if (scope == PlaytestOutboxScope.ReportsAndProgress) Count(directories.ProgressOutbox);
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
                foreach (var directory in EnumerateOutboxDirectories(outbox))
                {
                    var markers = ReadMarkers(directory);
                    if (!markers.Contains(ReadyMarker)) continue;
                    if (IsDelivered(markers)) continue;
                    // 印を書けない同名ディレクトリも失敗として画面へ残す
                    // A directory blocking the failure marker also remains visible as failed
                    var blockedMarker = Directory.Exists(Path.Combine(directory, FailedMarker));
                    if (blockedMarker) UnityEngine.Debug.LogWarning($"[PlaytestReceiver] failure marker is a directory: {directory}");
                    if (blockedMarker || markers.Contains(FailedMarker)) failed++;
                    else pending++;
                }
            }

            #endregion
        }

        private static string[] EnumerateOutboxDirectories(string outbox)
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

        private static string[] ReadMarkers(string directory)
        {
            // File.Existsは権限拒否もfalseへ畳むため、箱の読取成功を先に確定する
            // File.Exists folds access denial into false, so verify box enumeration before classifying markers
            return Directory.GetFiles(directory).Select(Path.GetFileName).ToArray();
        }

        private static bool IsDelivered(string[] markers)
        {
            return markers.Contains(UploadedMarker) || markers.Contains("SHIPPED");
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
