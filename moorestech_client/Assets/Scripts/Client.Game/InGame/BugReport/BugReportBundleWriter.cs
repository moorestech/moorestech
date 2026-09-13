using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording;
using Cysharp.Threading.Tasks;
using Game.Paths;
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

        private readonly IPlaytestSessionIdentity _identity;

        public BugReportBundleWriter(IPlaytestSessionIdentity identity)
        {
            _identity = identity;
        }

        public async UniTask<BugReportBundleResult> WriteAsync(BugReportCapturedData data, string description, string kind)
        {
            var directory = BugReportOutbox.CreateBundleDirectory(DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8));

            // Applicationのパス系はメインスレッドでしか読めないため、焼き込み情報とリポジトリの場所はここで先に読む
            // Application's path APIs are main-thread only, so the baked build info and repository roots are read here first
            var buildInfo = Application.isEditor ? null : RepositoryStateProbe.ReadBuildInfo();
            var repositoryRoot = RepositoryStateProbe.RepositoryRoot;
            var masterDataRoot = RepositoryStateProbe.MasterDataRoot;

            var manifest = new BugReportManifest
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                Description = description,
                Kind = kind,
                SteamId = _identity.SteamId,
                BuildInfo = buildInfo,
                Platform = Application.platform.ToString(),
                IsEditor = Application.isEditor,
                ReportTick = data.ReportTick,
                ClientState = data.ClientState,
                Missing = new List<MissingItem>(data.Missing),
            };

            // 既存消費側（BugReportRepositoryFiles）向けの射影。Editorではnullのまま渡し、従来どおりgit probe側の分岐へ通す
            // Projection for the existing consumer (BugReportRepositoryFiles); stays null in the Editor to keep taking the git-probe branch as before
            var buildInfoForFiles = Application.isEditor ? null : BuildInfoJson.ToBugReportBuildInfo(buildInfo);

            // ファイルコピーと ffmpeg はメインスレッドを塞がないようスレッドプールで行う
            // File copies and ffmpeg run on the thread pool so the main thread never blocks
            await UniTask.RunOnThreadPool(() =>
            {
                // ディスクは外部資源。1項目の失敗で他の資料まで巻き添えにしないよう項目ごとに隔離し、理由はmanifestと開発者ログの両方へ残す
                // Disk is an external resource; each item is isolated so one failure never takes the rest down, with the reason in both the manifest and the log
                // 握るのはディスク由来の失敗だけ。実装バグまで握ると障害と欠陥が同じ「欠損」表示に潰れて区別できなくなる
                // Only disk failures are caught; catching implementation bugs would collapse defects and outages into the same "missing" line
                // 2段の資料を書くCopyとWriteは内側で段ごとに隔離する。ここで一括して握ると、どちらが落ちたか分からないまま片方の名前で欠損が立つ
                // Copy and Write each produce two materials and isolate them inside; one catch here would blame a single name without knowing which stage failed
                BugReportWorldFilesCopier.Copy(data, directory, manifest);
                try { AssembleVideo(data, directory, manifest); } catch (Exception e) when (IsDiskFailure(e)) { manifest.AddMissing("video", $"組み立てに失敗した: {e.Message}"); }
                try { WriteFrameTicks(data, directory, manifest); } catch (Exception e) when (IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.FrameTicksFileName, $"書き出しに失敗した: {e.Message}"); }
                try { WriteLogs(data, directory, manifest); } catch (Exception e) when (IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.LogsDirectoryName, $"書き出しに失敗した: {e.Message}"); }
                try { CopyScreenshot(data, directory, manifest); } catch (Exception e) when (IsDiskFailure(e)) { manifest.AddMissing("screenshot", $"コピーに失敗した: {e.Message}"); }
                BugReportRepositoryFiles.Write(directory, manifest, buildInfoForFiles, repositoryRoot, masterDataRoot);
                ServerDataLocation.Record(data.ServerDataDirectory, manifest, repositoryRoot, masterDataRoot);
            });

            // manifestとREADYの書き出しも外部境界。ここが失敗した箱は運搬されないので必ず理由を残す
            // Writing the manifest and READY is an external boundary too; an unshipped box must always say why
            var ready = false;
            try
            {
                File.WriteAllText(Path.Combine(directory, BugReportBundleLayout.ManifestFileName), manifest.ToJson());
                BugReportOutbox.MarkReady(directory);
                ready = true;
                Debug.Log($"バグ報告バンドルを書きました {directory} missing:{manifest.Missing.Count}");
            }
            catch (Exception e) when (IsDiskFailure(e))
            {
                Debug.LogError($"バグ報告バンドルのmanifestを書けませんでした（この箱は運搬されません） {directory}: {e.Message}");
            }
            return new BugReportBundleResult { BundleDirectory = directory, Missing = manifest.Missing, Ready = ready };
        }

        private static void AssembleVideo(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (data.VideoSegmentFiles.Count == 0)
            {
                manifest.AddMissing("video", "録画の区間ファイルが1本も無かった");
                return;
            }

            var ffmpeg = FfmpegLocator.Find();
            var output = Path.Combine(directory, BugReportBundleLayout.VideoFileName);
            if (ffmpeg == null || !VideoAssembler.Concat(ffmpeg, data.VideoSegmentFiles, output))
            {
                manifest.AddMissing("video", ffmpeg == null ? "ffmpegが見つからなかった" : "区間の結合に失敗した");
                return;
            }

            // 尺を測れなかったときに0を書くと「0秒の動画」という実値になるので、欠損として残す
            // Writing 0 for an unmeasurable duration would bake "a zero-second video" as a real value, so it stays a missing item
            if (VideoAssembler.TryDurationSeconds(ffmpeg, output, out var videoSeconds)) manifest.VideoSeconds = videoSeconds;
            else manifest.AddMissing("videoSeconds", "結合した動画の尺を読み取れなかった");
            if (!VideoAssembler.ExtractFrames(ffmpeg, output, Path.Combine(directory, BugReportBundleLayout.FramesDirectoryName), 2)) manifest.AddMissing(BugReportBundleLayout.FramesDirectoryName, "静止画の抜き出しに失敗した");
        }

        private static void WriteFrameTicks(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (data.FrameTicks == null || data.FrameTicks.Count == 0)
            {
                manifest.AddMissing(BugReportBundleLayout.FrameTicksFileName, "フレームとtickの対応が1行も無かった");
                return;
            }
            File.WriteAllText(Path.Combine(directory, BugReportBundleLayout.FrameTicksFileName), FrameTickLog.ToTsv(data.FrameTicks));
        }

        private static void WriteLogs(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (data.Logs == null)
            {
                manifest.AddMissing(BugReportBundleLayout.LogsDirectoryName, "Unityログを確保できていなかった");
                return;
            }

            var logs = Path.Combine(directory, BugReportBundleLayout.LogsDirectoryName);
            Directory.CreateDirectory(logs);
            var builder = new StringBuilder();
            foreach (var entry in data.Logs)
            {
                builder.Append(entry.Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append('\t').Append(entry.Tick).Append('\t').Append(entry.Type).Append('\t').Append(entry.Message).Append('\n');
                if (entry.StackTrace.Length > 0) builder.Append(entry.StackTrace).Append('\n');
            }
            File.WriteAllText(Path.Combine(logs, BugReportBundleLayout.UnityLogFileName), builder.ToString());
        }

        private static void CopyScreenshot(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (string.IsNullOrEmpty(data.ScreenshotPath) || !File.Exists(data.ScreenshotPath))
            {
                manifest.AddMissing("screenshot", "確保時のスクリーンショットが無かった");
                return;
            }
            File.Copy(data.ScreenshotPath, Path.Combine(directory, BugReportBundleLayout.ScreenshotFileName));

            // 確保ごとの一時ファイルなので、バンドルへ写した時点で置き場に残さない
            // The capture-scoped temp file is removed once it has been copied into the bundle
            File.Delete(data.ScreenshotPath);
        }

        // 握ってよいのはディスク由来の失敗だけ。境界の根拠はAGENTS.mdの例外規定とD8裁定
        // Only disk-originated failures may be swallowed; the boundary rationale is AGENTS.md's exception rule and adjudication D8
        public static bool IsDiskFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException;
        }
    }
}
