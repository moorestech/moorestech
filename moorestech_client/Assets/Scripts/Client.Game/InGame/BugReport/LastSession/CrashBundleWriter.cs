using System;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.DiskOperations;
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
        // 起動ゲートから呼ばれるため、ここで例外を上へ投げるとゲートごと起動が壊れる。書けなかった場合はnullで理由をログに残す
        // Called from the startup gate; letting an exception escape here would break the gate itself, so an unwritable box logs its reason and returns null
        public async UniTask<string> WriteAsync(PreviousSessionArtifacts artifacts, string description)
        {
            // Applicationのパス系はメインスレッドでしか読めないため、スレッドプールへ出る前に読む
            // Application's path APIs are main-thread only, so they are read before leaving for the thread pool
            var repositoryRoot = RepositoryStateProbe.RepositoryRoot;
            var masterDataRoot = RepositoryStateProbe.MasterDataRoot;

            string bundleDirectory = null;

            // 退避物は録画とパケットログで数十MBに達する。兄弟のBugReportBundleWriterと同じくスレッドプールへ載せ、action処理スレッドを塞がない
            // The salvage reaches tens of megabytes of footage and packet logs; like its sibling BugReportBundleWriter it runs on the thread pool, never blocking the action thread
            await UniTask.RunOnThreadPool(() => { bundleDirectory = Write(artifacts, description, repositoryRoot, masterDataRoot); });
            return bundleDirectory;
        }

        // 書き出しの本体。スレッドプールからの復帰はPlayerLoop頼みで、CIのバッチモードEditModeテストでは戻れないため、同期で呼べる形に切り出す
        // The write itself; returning from the thread pool relies on the PlayerLoop, which CI's batch-mode EditMode tests never pump, so it is callable synchronously
        internal static string Write(PreviousSessionArtifacts artifacts, string description, string repositoryRoot, string masterDataRoot)
        {
            // 出所とSteamIDは落ちたセッション自身が開始時に書き残した値を載せる。今回起動したビルドを付けると別ビルドで再現される（F12）
            // The origin and SteamID come from what the crashed session wrote at its own start; attaching this boot's build would reproduce on a different build (F12)
            var origin = artifacts.PreviousOrigin;
            var buildOrigin = origin == null ? BuildOriginReading.WithoutInfo("前回セッションの出所の印が無いため、どのビルドで落ちたか分からない") : origin.BuildOrigin;
            var manifest = BugReportManifest.CreateHeader(description, PlaytestReportKind.Crash, origin?.SteamId, buildOrigin);
            manifest.Missing.AddRange(artifacts.Missing);

            string directory;
            // 箱の置き場作りはディスクIO。満杯や権限で失敗しても前回クラッシュの検知自体は続けたい
            // Creating the box's directory is disk IO; even if a full disk or missing permission fails it, detecting the earlier crash still proceeds
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
            var recordingMove = BugReportDiskOperations.MoveTree(artifacts.RecordingDirectory, Path.Combine(directory, BugReportBundleLayout.RecordingDirectoryName), out var movedRecordings);
            if (recordingMove.Succeeded) DeclareEmptySource(BugReportBundleLayout.RecordingDirectoryName, artifacts.RecordingDirectory, movedRecordings.Count);
            else manifest.AddMissing(BugReportBundleLayout.RecordingDirectoryName, recordingMove.FailureReason);

            MoveSnapshots(artifacts.SnapshotsDirectory, directory);

            try { CopyFileInto(artifacts.PlayerLogPath, Path.Combine(directory, BugReportBundleLayout.LogsDirectoryName)); }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.LogsDirectoryName, $"コピーに失敗した: {e.Message}"); }

            foreach (var dump in artifacts.CrashDumpFiles)
            {
                try { CopyFileInto(dump, Path.Combine(directory, BugReportBundleLayout.CrashDumpsDirectoryName)); }
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e)) { manifest.AddMissing(BugReportBundleLayout.CrashDumpsDirectoryName, $"コピーに失敗した: {e.Message}"); }
            }

            // リポジトリ状態とマスタの出所は bug の箱と同じ経路で入れる。crash だけ null だと再現側が別コミットで再生する
            // The repository state and master origin go through the same path as a bug box; leaving them null only for crash replays a different commit
            BugReportRepositoryFiles.Write(directory, manifest, buildOrigin, repositoryRoot, masterDataRoot);

            if (BugReportOutbox.TryFinishBundle(directory, manifest)) return directory;

            // 箱を閉じられなければ退避物を last-session へ戻す。戻さないと再送が空の退避元を素通りし、中身の無い箱をREADY付きで出荷する
            // A box that cannot be closed gives the salvage back to last-session; otherwise a retry sails past the emptied source and ships an empty box with READY on it
            CrashBundleSalvageMover.RestoreSalvageFromUnfinishedBundle(directory, artifacts);
            return null;

            #region Internal

            // 退避元が在るのに0件なのは、前回の書き出しで箱へ移し終えた跡。黙って通すと証跡が無いことを隠した箱が正式に出荷される
            // A source that exists yet yields nothing is the trace of an earlier write; passing it silently would ship a box that hides the absence of its evidence
            void DeclareEmptySource(string item, string source, int movedCount)
            {
                if (source == null || movedCount != 0 || !Directory.Exists(source)) return;
                manifest.AddMissing(item, "退避元が空だった（前回の書き出しで箱へ移動済みの可能性）");
            }

            // スナップショットとパケットログは plan B のプレイ報告と同じ snapshots/ 配下へ揃える（再現側の入口を1つに保つ）
            // Snapshots and packet logs land under the same snapshots/ as plan B's report, keeping one entry point for reproduction
            // 分類は綴りの正本（WorldDataDirectory）で行う。裸の接頭辞だと拡張子違いの別ファイルまで混ざる（F28）
            // Classification uses the spelling's single source (WorldDataDirectory); bare prefixes would sweep in other files that merely share the prefix (F28)
            void MoveSnapshots(string source, string boxDirectory)
            {
                var destination = Path.Combine(boxDirectory, BugReportBundleLayout.SnapshotDirectoryName);
                // 途中で転んでも移し終えた分は箱に入っているので、分類はその分だけ行い失敗は欠損として名乗る
                // A failure midway still leaves the moved files in the box, so those are classified and the failure is declared as a gap
                var move = BugReportDiskOperations.MoveTree(source, destination, out var moved);
                if (!move.Succeeded) manifest.AddMissing(BugReportBundleLayout.SnapshotDirectoryName, move.FailureReason);
                foreach (var relativePath in moved)
                {
                    var name = Path.GetFileName(relativePath);
                    if (WorldDataDirectory.TryParseSnapshotTick(name, out _)) manifest.SnapshotFiles.Add(relativePath);
                    if (WorldDataDirectory.TryParsePacketLogFromTick(name, out _)) manifest.PacketLogFiles.Add(relativePath);
                }
                if (move.Succeeded) DeclareEmptySource(BugReportBundleLayout.SnapshotDirectoryName, source, moved.Count);
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
    }
}
