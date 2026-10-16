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
            // silent-ok: 停滞がない再生は置き場もない / 箱を読む人はtrain-syncの不在を停滞なしと読む
            if (!Directory.Exists(sourceDirectory)) return;
            var item = BugReportBundleLayout.LogsDirectoryName + "/" + BugReportBundleLayout.TrainSyncDiagnosticsDirectoryName;

            FileInfo[] files;
            // 外部ディスク境界。列挙とメタデータ取得だけを隔離する
            // External disk boundary: isolate enumeration and metadata access only
            try
            {
                files = new DirectoryInfo(sourceDirectory).GetFiles("stall-*.json")
                    .OrderByDescending(file => file.LastWriteTimeUtc).ThenBy(file => file.Name).ToArray();
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
