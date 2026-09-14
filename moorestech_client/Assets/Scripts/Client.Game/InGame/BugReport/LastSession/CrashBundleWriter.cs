using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.Playtest;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // ゲートが依存する書き出し口。ゲートは「書けたか」しか見ないため、失敗の注入もこの1メソッドで足りる
    // The write port the gate depends on; the gate only observes whether a box was written, so one method carries failure injection too
    public interface ICrashBundleWriter
    {
        UniTask<string> WriteAsync(PreviousSessionArtifacts artifacts, string description);
    }

    // 退避物と説明文から kind=crash の箱を1つ書く。確保セッションが無い経路なので plan B の書き出しとは別物
    // Writes one kind=crash box from the salvaged files and the description; a path without a capture session, so it is separate from plan B's writer
    public sealed class CrashBundleWriter : ICrashBundleWriter
    {
        private readonly IPlaytestSessionIdentity _identity;

        public CrashBundleWriter(IPlaytestSessionIdentity identity)
        {
            _identity = identity;
        }

        // 起動ゲートから呼ばれるため、ここで例外を上へ投げるとゲートごと起動が壊れる。書けなかった場合はnullで理由をログに残す
        // Called from the startup gate; letting an exception escape here would break the gate itself, so an unwritable box logs its reason and returns null
        public async UniTask<string> WriteAsync(PreviousSessionArtifacts artifacts, string description)
        {
            // Applicationのパス系と焼き込み情報はメインスレッドでしか読めないため、スレッドプールへ出る前に読む
            // Application's path APIs and the baked build info are main-thread only, so they are read before leaving for the thread pool
            var manifest = BugReportManifest.CreateHeader(description, PlaytestReportKind.Crash, _identity.SteamId, RepositoryStateProbe.ReadBuildInfo());
            manifest.Missing = new List<MissingItem>(artifacts.Missing);
            var buildInfoForFiles = manifest.BuildInfo == null ? null : BuildInfoJson.ToBugReportBuildInfo(manifest.BuildInfo);
            var repositoryRoot = RepositoryStateProbe.RepositoryRoot;
            var masterDataRoot = RepositoryStateProbe.MasterDataRoot;

            string bundleDirectory = null;

            // 退避物は録画とパケットログで数十MBに達する。兄弟のBugReportBundleWriterと同じくスレッドプールへ載せ、action処理スレッドを塞がない
            // The salvage reaches tens of megabytes of footage and packet logs; like its sibling BugReportBundleWriter it runs on the thread pool, never blocking the action thread
            await UniTask.RunOnThreadPool(() => { bundleDirectory = WriteOnThreadPool(); });
            return bundleDirectory;

            #region Internal

            string WriteOnThreadPool()
            {
                string directory;
                try
                {
                    directory = BugReportOutbox.CreateBundleDirectory(BugReportOutbox.DefaultRootDirectory, DateTime.UtcNow, BugReportOutbox.CreateShortId());
                }
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
                {
                    Debug.LogError($"前回異常終了の箱の置き場を作れませんでした: {e.Message}");
                    return null;
                }

                // ディスクは外部資源。1項目の失敗で他の退避物まで巻き添えにしないよう項目ごとに隔離し、理由はmanifestと開発者ログの両方へ残す
                // Disk is an external resource; each item is isolated so one failure never takes the rest down, with the reason in both the manifest and the log
                try { DeclareEmptySource(BugReportBundleLayout.RecordingDirectoryName, artifacts.RecordingDirectory, MoveTree(artifacts.RecordingDirectory, Path.Combine(directory, BugReportBundleLayout.RecordingDirectoryName)).Count); }
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.RecordingDirectoryName, $"移動に失敗した: {e.Message}"); }

                try { MoveSnapshots(artifacts.SnapshotsDirectory, directory); }
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.SnapshotDirectoryName, $"移動に失敗した: {e.Message}"); }

                try { CopyFileInto(artifacts.PlayerLogPath, Path.Combine(directory, BugReportBundleLayout.LogsDirectoryName)); }
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.LogsDirectoryName, $"コピーに失敗した: {e.Message}"); }

                foreach (var dump in artifacts.CrashDumpFiles)
                {
                    try { CopyFileInto(dump, Path.Combine(directory, BugReportBundleLayout.CrashDumpsDirectoryName)); }
                    catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.CrashDumpsDirectoryName, $"コピーに失敗した: {e.Message}"); }
                }

                // リポジトリ状態とマスタの出所は bug の箱と同じ経路で入れる。crash だけ null だと再現側が別コミットで再生する
                // The repository state and master origin go through the same path as a bug box; leaving them null only for crash replays a different commit
                BugReportRepositoryFiles.Write(directory, manifest, buildInfoForFiles, repositoryRoot, masterDataRoot);

                if (BugReportOutbox.TryFinishBundle(directory, manifest)) return directory;

                // 箱を閉じられなければ退避物を last-session へ戻す。戻さないと再送が空の退避元を素通りし、中身の無い箱をREADY付きで出荷する
                // A box that cannot be closed gives the salvage back to last-session; otherwise a retry sails past the emptied source and ships an empty box with READY on it
                RestoreSalvageFromUnfinishedBundle(directory, artifacts);
                return null;
            }

            // 退避元が在るのに0件なのは、前回の書き出しで箱へ移し終えた跡。黙って通すと証跡が無いことを隠した箱が正式に出荷される
            // A source that exists yet yields nothing is the trace of an earlier write; passing it silently would ship a box that hides the absence of its evidence
            void DeclareEmptySource(string item, string source, int movedCount)
            {
                if (source == null || movedCount != 0 || !Directory.Exists(source)) return;
                manifest.AddMissing(item, "退避元が空だった（前回の書き出しで箱へ移動済みの可能性）");
            }

            // スナップショットとパケットログは plan B のプレイ報告と同じ snapshots/ 配下へ揃える（再現側の入口を1つに保つ）
            // Snapshots and packet logs land under the same snapshots/ as plan B's report, keeping one entry point for reproduction
            void MoveSnapshots(string source, string boxDirectory)
            {
                var destination = Path.Combine(boxDirectory, BugReportBundleLayout.SnapshotDirectoryName);
                var moved = MoveTree(source, destination);
                foreach (var relativePath in moved)
                {
                    var name = Path.GetFileName(relativePath);
                    if (name.StartsWith("tick_", StringComparison.Ordinal)) manifest.SnapshotFiles.Add(relativePath);
                    if (name.StartsWith("packets_", StringComparison.Ordinal)) manifest.PacketLogFiles.Add(relativePath);
                }
                DeclareEmptySource(BugReportBundleLayout.SnapshotDirectoryName, source, moved.Count);
            }

            // Player-prev.log とクラッシュダンプは Unity と OS が持つファイル。所有者から取り上げないよう写すだけにする
            // Player-prev.log and the crash dumps belong to Unity and the OS, so they are copied rather than taken away from their owner
            void CopyFileInto(string sourceFile, string destinationDirectory)
            {
                if (sourceFile == null || !File.Exists(sourceFile)) return;
                Directory.CreateDirectory(destinationDirectory);
                File.Copy(sourceFile, Path.Combine(destinationDirectory, Path.GetFileName(sourceFile)), true);
            }

            #endregion
        }

        // 箱を閉じられなかったときだけ、移した退避物を last-session へ戻す。裁定1の「送り直せる」を実際に成立させる唯一の経路
        // Only when the box could not be closed does the moved salvage go back to last-session; this is what actually makes adjudication 1's "you can resend" true
        internal static void RestoreSalvageFromUnfinishedBundle(string bundleDirectory, PreviousSessionArtifacts artifacts)
        {
            RestoreTree(Path.Combine(bundleDirectory, BugReportBundleLayout.RecordingDirectoryName), artifacts.RecordingDirectory);
            RestoreTree(Path.Combine(bundleDirectory, BugReportBundleLayout.SnapshotDirectoryName), artifacts.SnapshotsDirectory);

            #region Internal

            void RestoreTree(string boxSubDirectory, string salvageDirectory)
            {
                if (salvageDirectory == null || !Directory.Exists(boxSubDirectory)) return;
                try
                {
                    var restored = MoveTree(boxSubDirectory, salvageDirectory);
                    Debug.LogWarning($"箱を閉じられなかったため退避物を戻しました（次の送信で送り直せます） {salvageDirectory} files:{restored.Count}");
                }
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
                {
                    Debug.LogError($"退避物を last-session へ戻せませんでした（未完成の箱 {boxSubDirectory} に残っています）: {e.Message}");
                }
            }

            #endregion
        }

        // 退避先から箱へは移動で渡す。退避の時点で既に last-session へ改名済みなので、写すと同じ数十MBを2度書くだけになる
        // The salvage moves into the box: it was already renamed into last-session, so copying would write the same tens of megabytes twice
        // コピーへ戻せば再送は成立するが二重書き込みが復活するため、失敗時だけ戻す形で「送り直せる」を満たす
        // Reverting to a copy would also make the resend work, but it brings back the double write, so restoring only on failure buys the same guarantee
        // 退避は pid_<PID>/ 等の入れ子を保ったまま移すため、こちらも入れ子ごと辿る。戻り値は移した相対パス
        // The salvage keeps nesting such as pid_<PID>/, so this walks the whole tree too; the relative paths moved are returned
        private static IReadOnlyList<string> MoveTree(string source, string destination)
        {
            var moved = new List<string>();
            if (source == null || !Directory.Exists(source)) return moved;

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(source, file);
                var destinationFile = Path.Combine(destination, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Move(file, destinationFile);
                moved.Add(relativePath);
            }
            foreach (var subDirectory in Directory.GetDirectories(source)) Directory.Delete(subDirectory, true);
            return moved;
        }
    }
}
