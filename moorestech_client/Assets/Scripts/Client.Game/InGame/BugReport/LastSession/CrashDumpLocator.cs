using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // Unityが残すクラッシュダンプの置き場。Windowsは %LOCALAPPDATA%\Temp\<company>\<product>\Crashes が既定
    // Where Unity leaves crash dumps; on Windows the default is %LOCALAPPDATA%\Temp\<company>\<product>\Crashes
    internal static class CrashDumpLocator
    {
        internal const string CrashesFolderName = "Crashes";

        // Editorのプロセス名。Editor起動のクラッシュを自分の記録として拾うための正本
        // The Editor's process name; the single source for recognizing an Editor boot's crash as ours
        private const string EditorProcessName = "Unity";

        // 前回セッションの時刻境界が取れないときだけ使う保険の窓。実在する境界が取れるならそちらが常に優先される
        // A fallback window used only when no real boundary for the previous session exists; a real one always wins
        private const int FallbackLookbackHours = -24;

        // どの置き場が共有かの宣言そのものが絞り込みの要。宣言を落とすと他アプリのダンプが素通りするためテストから見える形で置く
        // The shared/dedicated declaration is the filter itself: dropping it lets other apps' dumps through, so tests can read it
        internal static IReadOnlyList<CrashDumpRoot> CandidateDumpRoots()
        {
            // Client.Game.InGame.Environment（地形namespace）と同名衝突するため System.Environment を完全修飾する
            // Fully-qualified as System.Environment to avoid colliding with the sibling Client.Game.InGame.Environment namespace
            var roots = new List<CrashDumpRoot>();
            var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                // Windowsの2候補は<product>配下の専用置き場なので、中身は必ず自分のクラッシュだけ
                // Both Windows candidates live under <product>, so whatever they hold is this app's crash and nothing else
                roots.Add(new CrashDumpRoot { Path = Path.Combine(localAppData, "Temp", UnityEngine.Application.companyName, UnityEngine.Application.productName, CrashesFolderName) });
                roots.Add(new CrashDumpRoot { Path = Path.Combine(localAppData, UnityEngine.Application.companyName, UnityEngine.Application.productName, CrashesFolderName) });
            }

            // macOS はクラッシュレポータのdiagnosticsに残る。Windows検証機と同じ入口で拾えるよう並べておく
            // macOS keeps them in the crash reporter's diagnostics folder; listed here so one entry point covers both
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) roots.Add(new CrashDumpRoot { Path = Path.Combine(home, "Library", "Logs", "DiagnosticReports"), SharedWithOtherApps = true });
            return roots;
        }

        // 前回セッションが実在した時刻より後のダンプだけを拾う。境界を「直近24時間」で代用すると他アプリの古い記録まで入る
        // Takes only dumps newer than when the previous session actually existed; a "last 24 hours" stand-in would sweep in other apps' old records
        internal static CrashDumpScanResult FindDumpFiles()
        {
            var since = PreviousSessionBoundaryUtc();
            var candidates = new List<CrashDumpCandidate>();
            foreach (var root in CandidateDumpRoots()) CollectFrom(root);

            var result = SelectDumpFiles(candidates, UnityEngine.Application.productName);
            LogExclusion();
            return result;

            #region Internal

            // 前回セッションのログの最終更新時刻が「そのセッションが確かに動いていた」唯一の実在する印
            // The previous session log's last write is the only real evidence of when that session was actually running
            DateTime PreviousSessionBoundaryUtc()
            {
                var previousLogPath = PlayerLogLocator.PreviousSessionLogPath();
                if (previousLogPath == null) return DateTime.UtcNow.AddHours(FallbackLookbackHours);
                return new FileInfo(previousLogPath).LastWriteTimeUtc;
            }

            // 共有置き場の走査はOSの保護領域（macOSのDiagnosticReportsはTCC配下）に触れる外部境界。拒否されても起動は続ける
            // Scanning a shared root touches an OS-protected area (macOS DiagnosticReports sits under TCC); a refusal must not stop the boot
            void CollectFrom(CrashDumpRoot root)
            {
                if (!Directory.Exists(root.Path)) return;
                try
                {
                    foreach (var file in Directory.GetFiles(root.Path, "*", SearchOption.AllDirectories))
                    {
                        var info = new FileInfo(file);
                        if (info.LastWriteTimeUtc < since) continue;
                        candidates.Add(new CrashDumpCandidate { Root = root, FileName = info.Name, FullPath = file });
                    }
                }
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
                {
                    Debug.LogWarning($"クラッシュダンプの置き場を読めませんでした（この置き場は対象外になります） {root.Path}: {e.Message}");
                }
            }

            // 落としたことは必ず開発者ログへ出す。無音で捨てると「見つからない」と「捨てた」が区別できなくなる
            // Every drop reaches the developer log; a silent drop makes "none found" and "filtered out" indistinguishable
            void LogExclusion()
            {
                if (result.ExcludedAsOtherApps == 0) return;
                var condition = $"ファイル名が {UnityEngine.Application.productName}- または {EditorProcessName}- で始まること";
                Debug.Log($"共有置き場のクラッシュレポート{result.ExcludedAsOtherApps}件を他アプリのものとして除外しました 条件:{condition} 除外元:{string.Join(", ", result.ExcludedRoots)}");
            }

            #endregion
        }

        // 「そもそも無かった」と「他アプリとして除外した結果0件」を読み分けられる理由文にする。無音の縮退を残さない
        // The reason distinguishes "there were none" from "all were filtered out as other apps'", leaving no silent degradation
        internal static string MissingReason(CrashDumpScanResult scan)
        {
            var roots = string.Join(", ", CandidateRoots());
            if (scan.ExcludedAsOtherApps == 0) return $"クラッシュダンプが見つからない（探索先: {roots}）";
            return $"共有置き場に{scan.ExcludedAsOtherApps}件あったが自プロセス（{UnityEngine.Application.productName} / {EditorProcessName}）のものは0件だった（除外元: {string.Join(", ", scan.ExcludedRoots)}、探索先: {roots}）";

            #region Internal

            IReadOnlyList<string> CandidateRoots()
            {
                var paths = new List<string>();
                foreach (var root in CandidateDumpRoots()) paths.Add(root.Path);
                return paths;
            }

            #endregion
        }

        // 置き場の共有宣言だけを見て選別する純粋関数。実ファイルを置かずに配線ごと検証できる
        // A pure selection driven only by the roots' shared declaration, so the wiring is verifiable without real files
        internal static CrashDumpScanResult SelectDumpFiles(IReadOnlyList<CrashDumpCandidate> candidates, string productName)
        {
            var result = new CrashDumpScanResult();
            foreach (var candidate in candidates)
            {
                if (!IsDumpLikeName(candidate.FileName)) continue;
                if (candidate.Root.SharedWithOtherApps && !IsOwnProcessDumpName(candidate.FileName, productName))
                {
                    result.ExcludedAsOtherApps++;
                    if (!result.ExcludedRoots.Contains(candidate.Root.Path)) result.ExcludedRoots.Add(candidate.Root.Path);
                    continue;
                }
                result.Files.Add(candidate.FullPath);
            }
            return result;

            #region Internal

            bool IsDumpLikeName(string fileName)
            {
                var name = fileName.ToLowerInvariant();
                return name.EndsWith(".dmp") || name.EndsWith(".crash") || name.EndsWith(".ips") || name == "error.log";
            }

            #endregion
        }

        // 共有置き場のクラッシュレポートは <プロセス名>-<日付>.ips 形式。プロセス名ちょうどで始まるものだけを自分の記録として扱う
        // A shared root's report is named <process>-<date>.ips, so only names starting with the exact process name count as ours
        // '-'で切ると、プロセス名自体にハイフンを含む製品が自分のダンプを他アプリとして捨てる
        // Splitting on '-' would make a product whose own name contains a hyphen discard its own dumps as another app's
        internal static bool IsOwnProcessDumpName(string fileName, string productName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            // Editor起動のプロセス名はUnity。テスターのビルドは productName なので両方を自分として認める
            // An Editor boot's process is Unity while a tester's build is productName, so both count as ours
            return StartsWithProcessName(productName) || StartsWithProcessName(EditorProcessName);

            #region Internal

            bool StartsWithProcessName(string processName)
            {
                return !string.IsNullOrEmpty(processName) && fileName.StartsWith(processName + "-", StringComparison.OrdinalIgnoreCase);
            }

            #endregion
        }
    }
}
