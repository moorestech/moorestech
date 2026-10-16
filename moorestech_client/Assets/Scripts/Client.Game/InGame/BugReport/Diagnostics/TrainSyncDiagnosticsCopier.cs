using System;
using System.IO;
using System.Linq;
using Game.Paths;

namespace Client.Game.InGame.BugReport.Diagnostics
{
    public static class TrainSyncDiagnosticsCopier
    {
        private const int MaxFiles = 8;

        public static void Copy(string sourceDirectory, string bundleDirectory, BugReportManifest manifest)
        {
            var item = BugReportBundleLayout.LogsDirectoryName + "/" + BugReportBundleLayout.TrainSyncDiagnosticsDirectoryName;

            FileInfo[] files;
            // 外部ディスク境界。列挙とメタデータ取得だけを隔離する
            // External disk boundary: isolate enumeration and metadata access only
            try
            {
                files = new DirectoryInfo(sourceDirectory).GetFiles("stall-*.json")
                    .OrderByDescending(file => file.LastWriteTimeUtc).ThenBy(file => file.Name).ToArray();
            }
            catch (DirectoryNotFoundException)
            {
                return; // silent-ok: 未作成の再生には停滞診断がない / 箱のtrain-sync不在で分かる
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                manifest.AddMissing(item, e.Message);
                return;
            }
            if (MaxFiles < files.Length)
                manifest.AddMissing(item, $"停滞診断が多すぎるため新しい{MaxFiles}件だけ入れた（全{files.Length}件）");

            // 個別コピーの外部ディスク失敗を欠損として報告する
            // Report each external disk copy failure as a missing evidence item
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
    }
}
