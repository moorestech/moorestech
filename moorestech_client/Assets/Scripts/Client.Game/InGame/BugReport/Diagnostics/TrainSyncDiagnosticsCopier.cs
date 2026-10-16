using System;
using System.IO;
using System.Linq;
using Game.Paths;

namespace Client.Game.InGame.BugReport.Diagnostics
{
    public static class TrainSyncDiagnosticsCopier
    {
        public const int MaxFiles = 8;

        public static void Copy(string sourceDirectory, DateTime playStartedAtUtc, string bundleDirectory, BugReportManifest manifest)
        {
            // silent-ok: 停滞がない再生は置き場もない / 箱を読む人はtrain-syncの不在を停滞なしと読む
            if (!Directory.Exists(sourceDirectory)) return;
            var item = BugReportBundleLayout.LogsDirectoryName + "/" + BugReportBundleLayout.TrainSyncDiagnosticsDirectoryName;

            // 診断ファイルは外部ディスク資源。列挙失敗も個別コピー失敗も欠損として報告する
            // Diagnostic files are external disk resources; report enumeration and individual copy failures as gaps
            try
            {
                var files = new DirectoryInfo(sourceDirectory).GetFiles("stall-*.json")
                    .Where(file => file.LastWriteTimeUtc >= playStartedAtUtc)
                    .OrderByDescending(file => file.LastWriteTimeUtc).ThenBy(file => file.Name).ToArray();
                if (files.Length > MaxFiles)
                    manifest.AddMissing(item, $"停滞診断が多すぎるため新しい{MaxFiles}件だけ入れた（全{files.Length}件）");

                foreach (var file in files.Take(MaxFiles))
                {
                    try
                    {
                        var destination = Path.Combine(bundleDirectory, item);
                        Directory.CreateDirectory(destination);
                        File.Copy(file.FullName, Path.Combine(destination, file.Name));
                    }
                    catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
                    {
                        manifest.AddMissing(item + "/" + file.Name, e.Message);
                    }
                }
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                manifest.AddMissing(item, e.Message);
            }
        }
    }
}
