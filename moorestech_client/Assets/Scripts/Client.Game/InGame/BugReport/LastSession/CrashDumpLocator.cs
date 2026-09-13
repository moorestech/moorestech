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

        // 共有置き場はOSが全アプリのクラッシュを溜める場所で、自プロセス名で絞らないと他アプリの記録まで運んでしまう
        // A shared root is where the OS piles every app's crash, so without a process-name filter other apps' records get shipped
        public sealed class DumpRoot
        {
            public string Path;
            public bool SharedWithOtherApps;
        }

        public static IReadOnlyList<string> CandidateRoots()
        {
            var roots = new List<string>();
            foreach (var root in CandidateDumpRoots()) roots.Add(root.Path);
            return roots;
        }

        private static IReadOnlyList<DumpRoot> CandidateDumpRoots()
        {
            // Client.Game.InGame.Environment（地形namespace）と同名衝突するため System.Environment を完全修飾する
            // Fully-qualified as System.Environment to avoid colliding with the sibling Client.Game.InGame.Environment namespace
            // Client.Game.InGame.Environment（地形namespace）と同名衝突するため System.Environment を完全修飾する
            // Fully-qualified as System.Environment to avoid colliding with the sibling Client.Game.InGame.Environment namespace
            var roots = new List<DumpRoot>();
            var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                // Windowsの2候補は<product>配下の専用置き場なので、中身は必ず自分のクラッシュだけ
                // Both Windows candidates live under <product>, so whatever they hold is this app's crash and nothing else
                roots.Add(new DumpRoot { Path = Path.Combine(localAppData, "Temp", UnityEngine.Application.companyName, UnityEngine.Application.productName, CrashesFolderName) });
                roots.Add(new DumpRoot { Path = Path.Combine(localAppData, UnityEngine.Application.companyName, UnityEngine.Application.productName, CrashesFolderName) });
            }

            // macOS はクラッシュレポータのdiagnosticsに残る。Windows検証機と同じ入口で拾えるよう並べておく
            // macOS keeps them in the crash reporter's diagnostics folder; listed here so one entry point covers both
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) roots.Add(new DumpRoot { Path = Path.Combine(home, "Library", "Logs", "DiagnosticReports"), SharedWithOtherApps = true });
            return roots;
        }

        // 直近24時間ぶんだけを拾う。過去の無関係なダンプで箱を膨らませない
        // Takes only the last 24 hours so unrelated old dumps never inflate the box
        public static List<string> FindDumpFiles()
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var found = new List<string>();
            foreach (var root in CandidateDumpRoots())
            {
                if (!Directory.Exists(root.Path)) continue;
                foreach (var file in Directory.GetFiles(root.Path, "*", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTimeUtc < since) continue;
                    if (!IsDumpLikeName(info.Name)) continue;
                    if (root.SharedWithOtherApps && !IsOwnProcessDumpName(info.Name, UnityEngine.Application.productName)) continue;
                    found.Add(file);
                }
            }
            return found;
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
