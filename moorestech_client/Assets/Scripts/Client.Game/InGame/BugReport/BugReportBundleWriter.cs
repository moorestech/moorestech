using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Recording;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    public sealed class BugReportBundleResult
    {
        public string BundleDirectory;
        public IReadOnlyList<MissingItem> Missing;

        // manifestとREADYまで書けたか。falseの箱は運搬されないので送信側は成功として扱ってはならない
        // Whether the manifest and READY were written; an unshipped box must never be reported as a success
        public bool Ready;
    }

    // 確保済みの記録を outbox の1箱へ書く。欠けた項目は manifest.missing に理由付きで残し、例外で止めない
    // Writes the captured records into one outbox box; missing items go to manifest.missing with reasons, never throwing
    public sealed class BugReportBundleWriter
    {
        public const long UntrackedBytesLimit = 20L * 1024 * 1024;

        public async UniTask<BugReportBundleResult> WriteAsync(BugReportCapturedData data, string description)
        {
            var directory = BugReportOutbox.CreateBundleDirectory(DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8));
            var manifest = new BugReportManifest
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                Description = description,
                Platform = Application.platform.ToString(),
                IsEditor = Application.isEditor,
                ReportTick = data.ReportTick,
                ClientState = data.ClientState,
                Missing = new List<MissingItem>(data.Missing),
            };

            // Applicationのパス系はメインスレッドでしか読めないため、焼き込み情報とリポジトリの場所はここで先に読む
            // Application's path APIs are main-thread only, so the baked build info and repository roots are read here first
            var buildInfo = Application.isEditor ? null : RepositoryStateProbe.ReadBuildInfo();
            var repositoryRoot = RepositoryStateProbe.RepositoryRoot;
            var masterDataRoot = RepositoryStateProbe.MasterDataRoot;

            // ファイルコピーと ffmpeg はメインスレッドを塞がないようスレッドプールで行う
            // File copies and ffmpeg run on the thread pool so the main thread never blocks
            await UniTask.RunOnThreadPool(() =>
            {
                // ファイル操作は外部境界。1項目の失敗で他の資料まで巻き添えにしないよう項目ごとに隔離する
                // File operations are an external boundary; each item is isolated so one failure never takes the rest down
                try { CopyWorld(data, directory, manifest); } catch (Exception e) { manifest.AddMissing("snapshots", $"コピーに失敗した: {e.GetBaseException().Message}"); }
                try { AssembleVideo(data, directory, manifest); } catch (Exception e) { manifest.AddMissing("video", $"組み立てに失敗した: {e.GetBaseException().Message}"); }
                try { WriteFrameTicks(data, directory, manifest); } catch (Exception e) { manifest.AddMissing("frames.tsv", $"書き出しに失敗した: {e.GetBaseException().Message}"); }
                try { WriteLogs(data, directory, manifest); } catch (Exception e) { manifest.AddMissing("logs", $"書き出しに失敗した: {e.GetBaseException().Message}"); }
                try { CopyScreenshot(data, directory, manifest); } catch (Exception e) { manifest.AddMissing("screenshot", $"コピーに失敗した: {e.GetBaseException().Message}"); }
                try { BugReportRepositoryFiles.Write(directory, manifest, buildInfo, repositoryRoot, masterDataRoot); } catch (Exception e) { manifest.AddMissing("repo", $"リポジトリ状態の書き出しに失敗した: {e.GetBaseException().Message}"); }
                ServerDataLocation.Record(data.ServerDataDirectory, manifest, repositoryRoot, masterDataRoot);
            });

            // manifestとREADYの書き出しも外部境界。ここが失敗した箱は運搬されないので必ず理由を残す
            // Writing the manifest and READY is an external boundary too; an unshipped box must always say why
            var ready = false;
            try
            {
                File.WriteAllText(Path.Combine(directory, "manifest.json"), manifest.ToJson());
                BugReportOutbox.MarkReady(directory);
                ready = true;
                Debug.Log($"バグ報告バンドルを書きました {directory} missing:{manifest.Missing.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"バグ報告バンドルのmanifestを書けませんでした（この箱は運搬されません） {directory}: {e.GetBaseException().Message}");
            }
            return new BugReportBundleResult { BundleDirectory = directory, Missing = manifest.Missing, Ready = ready };
        }

        private static void CopyWorld(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (string.IsNullOrEmpty(data.SnapshotDirectory))
            {
                manifest.AddMissing("snapshots", "サーバーのスナップショット置き場が分からなかった");
                return;
            }

            var snapshots = Path.Combine(directory, "snapshots");
            Directory.CreateDirectory(snapshots);
            foreach (var name in data.SnapshotFileNames.Concat(data.PacketLogFileNames))
            {
                var source = Path.Combine(data.SnapshotDirectory, name);
                if (!File.Exists(source))
                {
                    manifest.AddMissing(name, "スナップショットディレクトリに無かった");
                    continue;
                }
                File.Copy(source, Path.Combine(snapshots, name));
            }
            manifest.SnapshotFiles = data.SnapshotFileNames.ToList();
            manifest.PacketLogFiles = data.PacketLogFileNames.ToList();
            manifest.SnapshotTicks = data.SnapshotFileNames.Select(ParseTick).Where(tick => tick > 0).OrderBy(tick => tick).ToList();

            // 再現にはスナップショット本体だけでなく、その隣のワールド定義（地図・世界メタ）が要る
            // Reproduction needs the world definition beside the snapshots (map and world meta), not just the snapshots
            var worldRoot = Path.GetDirectoryName(data.SnapshotDirectory);
            var world = Path.Combine(directory, "world");
            Directory.CreateDirectory(world);
            foreach (var name in new[] { "world.json", "map.json" })
            {
                var source = Path.Combine(worldRoot, name);
                if (File.Exists(source)) File.Copy(source, Path.Combine(world, name));
                else manifest.AddMissing(name, "ワールドディレクトリに無かった");
            }
        }

        private static void AssembleVideo(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (data.VideoSegmentFiles.Count == 0)
            {
                manifest.AddMissing("video", "録画の区間ファイルが1本も無かった");
                return;
            }

            var ffmpeg = FfmpegLocator.Find();
            var output = Path.Combine(directory, "video.mp4");
            if (ffmpeg == null || !VideoAssembler.Concat(ffmpeg, data.VideoSegmentFiles, output))
            {
                manifest.AddMissing("video", ffmpeg == null ? "ffmpegが見つからなかった" : "区間の結合に失敗した");
                return;
            }

            manifest.VideoSeconds = VideoAssembler.DurationSeconds(ffmpeg, output);
            if (!VideoAssembler.ExtractFrames(ffmpeg, output, Path.Combine(directory, "frames"), 2)) manifest.AddMissing("frames", "静止画の抜き出しに失敗した");
        }

        private static void WriteFrameTicks(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (data.FrameTicks == null || data.FrameTicks.Count == 0)
            {
                manifest.AddMissing("frames.tsv", "フレームとtickの対応が1行も無かった");
                return;
            }
            File.WriteAllText(Path.Combine(directory, "frames.tsv"), FrameTickLog.ToTsv(data.FrameTicks));
        }

        private static void WriteLogs(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (data.Logs == null)
            {
                manifest.AddMissing("logs", "Unityログを確保できていなかった");
                return;
            }

            var logs = Path.Combine(directory, "logs");
            Directory.CreateDirectory(logs);
            var builder = new StringBuilder();
            foreach (var entry in data.Logs)
            {
                builder.Append(entry.Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append('\t').Append(entry.Tick).Append('\t').Append(entry.Type).Append('\t').Append(entry.Message).Append('\n');
                if (entry.StackTrace.Length > 0) builder.Append(entry.StackTrace).Append('\n');
            }
            File.WriteAllText(Path.Combine(logs, "unity.log"), builder.ToString());
        }

        private static void CopyScreenshot(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (string.IsNullOrEmpty(data.ScreenshotPath) || !File.Exists(data.ScreenshotPath))
            {
                manifest.AddMissing("screenshot", "確保時のスクリーンショットが無かった");
                return;
            }
            File.Copy(data.ScreenshotPath, Path.Combine(directory, "screenshot.png"));

            // 確保ごとの一時ファイルなので、バンドルへ写した時点で置き場に残さない
            // The capture-scoped temp file is removed once it has been copied into the bundle
            File.Delete(data.ScreenshotPath);
        }

        private static ulong ParseTick(string fileName)
        {
            var core = fileName.Replace("tick_", "").Replace(".json", "");
            return ulong.TryParse(core, out var tick) ? tick : 0;
        }
    }
}
