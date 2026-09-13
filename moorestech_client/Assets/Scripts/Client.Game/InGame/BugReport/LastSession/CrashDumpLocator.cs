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

        public static IReadOnlyList<string> CandidateRoots()
        {
            // Client.Game.InGame.Environment（地形namespace）と同名衝突するため System.Environment を完全修飾する
            // Fully-qualified as System.Environment to avoid colliding with the sibling Client.Game.InGame.Environment namespace
            var roots = new List<string>();
            var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                roots.Add(Path.Combine(localAppData, "Temp", UnityEngine.Application.companyName, UnityEngine.Application.productName, CrashesFolderName));
                roots.Add(Path.Combine(localAppData, UnityEngine.Application.companyName, UnityEngine.Application.productName, CrashesFolderName));
            }

            // macOS はクラッシュレポータのdiagnosticsに残る。Windows検証機と同じ入口で拾えるよう並べておく
            // macOS keeps them in the crash reporter's diagnostics folder; listed here so one entry point covers both
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) roots.Add(Path.Combine(home, "Library", "Logs", "DiagnosticReports"));
            return roots;
        }

        // 直近24時間ぶんだけを拾う。過去の無関係なダンプで箱を膨らませない
        // Takes only the last 24 hours so unrelated old dumps never inflate the box
        public static List<string> FindDumpFiles()
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var found = new List<string>();
            foreach (var root in CandidateRoots())
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTimeUtc < since) continue;
                    if (!IsDumpLikeName(info.Name)) continue;
                    found.Add(file);
                }
            }
            return found;
        }

        private static bool IsDumpLikeName(string fileName)
        {
            var name = fileName.ToLowerInvariant();
            return name.EndsWith(".dmp") || name.EndsWith(".crash") || name.EndsWith(".ips") || name == "error.log";
        }
    }
}
