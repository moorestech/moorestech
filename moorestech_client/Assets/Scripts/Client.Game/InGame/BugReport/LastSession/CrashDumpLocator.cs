using System;
using System.Collections.Generic;
using System.IO;

namespace Client.Game.InGame.BugReport.LastSession
{
    // Unityが残すクラッシュダンプの置き場。Windowsは %LOCALAPPDATA%\Temp\<company>\<product>\Crashes が既定
    // Where Unity leaves crash dumps; on Windows the default is %LOCALAPPDATA%\Temp\<company>\<product>\Crashes
    public static class CrashDumpLocator
    {
        public const string CrashesFolderName = "Crashes";

        // Editorのプロセス名。Editor起動のクラッシュを自分の記録として拾うための正本
        // The Editor's process name; the single source for recognizing an Editor boot's crash as ours
        public const string EditorProcessName = "Unity";

        public static IReadOnlyList<string> CandidateRoots()
        {
            var roots = new List<string>();
            foreach (var root in CandidateDumpRoots()) roots.Add(root.Path);
            return roots;
        }

        // どの置き場が共有かの宣言そのものが絞り込みの要。宣言を落とすと他アプリのダンプが素通りするためテストから見える形で置く
        // The shared/dedicated declaration is the filter itself: dropping it lets other apps' dumps through, so tests can read it
        public static IReadOnlyList<CrashDumpRoot> CandidateDumpRoots()
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

        // 直近24時間ぶんだけを拾う。過去の無関係なダンプで箱を膨らませない
        // Takes only the last 24 hours so unrelated old dumps never inflate the box
        public static CrashDumpScanResult FindDumpFiles()
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var candidates = new List<CrashDumpCandidate>();
            foreach (var root in CandidateDumpRoots())
            {
                if (!Directory.Exists(root.Path)) continue;
                foreach (var file in Directory.GetFiles(root.Path, "*", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTimeUtc < since) continue;
                    candidates.Add(new CrashDumpCandidate { Root = root, FileName = info.Name, FullPath = file });
                }
            }

            var result = SelectDumpFiles(candidates, UnityEngine.Application.productName);
            LogExclusion(result);
            return result;
        }

        // 置き場の共有宣言だけを見て選別する純粋関数。実ファイルを置かずに配線ごと検証できる
        // A pure selection driven only by the roots' shared declaration, so the wiring is verifiable without real files
        public static CrashDumpScanResult SelectDumpFiles(IReadOnlyList<CrashDumpCandidate> candidates, string productName)
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
        }

        // 落としたことは必ず開発者ログへ出す。無音で捨てると「見つからない」と「捨てた」が区別できなくなる
        // Every drop reaches the developer log; a silent drop makes "none found" and "filtered out" indistinguishable
        private static void LogExclusion(CrashDumpScanResult result)
        {
            if (result.ExcludedAsOtherApps == 0) return;
            var condition = $"ファイル名が {UnityEngine.Application.productName}- または {EditorProcessName}- で始まること";
            UnityEngine.Debug.Log($"共有置き場のクラッシュレポート{result.ExcludedAsOtherApps}件を他アプリのものとして除外しました 条件:{condition} 除外元:{string.Join(", ", result.ExcludedRoots)}");
        }

        // 共有置き場のクラッシュレポートは <プロセス名>-<日付>.ips 形式。自分のプロセス名で始まるものだけを自分の記録として扱う
        // A shared root's report is named <process>-<date>.ips, so only names starting with our own process count as ours
        public static bool IsOwnProcessDumpName(string fileName, string productName)
        {
            var processName = fileName.Split('-')[0];
            if (processName.Length == 0) return false;

            // Editor起動のプロセス名はUnity。テスターのビルドは productName なので両方を自分として認める
            // An Editor boot's process is Unity while a tester's build is productName, so both count as ours
            return string.Equals(processName, productName, StringComparison.OrdinalIgnoreCase) || string.Equals(processName, EditorProcessName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDumpLikeName(string fileName)
        {
            var name = fileName.ToLowerInvariant();
            return name.EndsWith(".dmp") || name.EndsWith(".crash") || name.EndsWith(".ips") || name == "error.log";
        }
    }
}
