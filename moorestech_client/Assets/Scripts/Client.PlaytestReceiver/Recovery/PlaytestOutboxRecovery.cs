using System;
using System.IO;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    // バグ報告画面からの再送・書き出しの入口。outboxへの読み書きは配送と同じ門で直列化する
    // Entry point for resend and export from the bug-report page; outbox access shares the delivery gate
    public sealed class PlaytestOutboxRecovery
    {
        private readonly IPlaytestUploadRequester _uploadRequester;
        private readonly PlaytestOutboxDirectories _directories;
        private readonly IPlaytestExportDestination _destination;
        private readonly IPlaytestFileReveal _reveal;

        public PlaytestOutboxRecovery(IPlaytestUploadRequester uploadRequester, PlaytestOutboxDirectories directories, IPlaytestExportDestination destination, IPlaytestFileReveal reveal)
        {
            _uploadRequester = uploadRequester;
            _directories = directories;
            _destination = destination;
            _reveal = reveal;
        }

        public void RequestResendFailed()
        {
            ResendFailedAsync(CurrentScope()).Forget();
        }

        public void RequestExportUnsent()
        {
            ExportUnsentAsync(CurrentScope()).Forget();
        }

        // 画面の件数と同じ範囲を対象にするため、現在の配送経路から範囲を引く
        // Use the current delivery route's scope so the targets match the displayed counts
        private static PlaytestOutboxScope CurrentScope()
        {
            return PlaytestDeliveryRouteResolver.ResolveCurrent().Scope;
        }

        internal async UniTask ResendFailedAsync(PlaytestOutboxScope scope)
        {
            // 走行中の配送が終わるまで待ってから印を外す。走行が箱を数えている最中に印を変えない
            // Wait for any in-flight run before unmarking so markers never change under a counting run
            await PlaytestOutboxAccess.Gate.WaitAsync(Application.exitCancellationToken);
            try { PlaytestFailedBoxRelease.ReleaseAll(_directories, scope); }
            finally { PlaytestOutboxAccess.Gate.Release(); }
            // 解除できた数に関わらず要求する。走行が件数表示を最新にする
            // Request regardless of the released count; the run refreshes the displayed counts
            _uploadRequester.RequestUpload();
        }

        private async UniTask ExportUnsentAsync(PlaytestOutboxScope scope)
        {
            var zipPath = BeginExport();
            if (zipPath == null) return;
            var exportedAt = DateTime.UtcNow;
            // 想定外の例外で抜けても書き出し中のまま固めない。理由はForgetのログとPlayer.logに出る
            // Never stay frozen in running after an unexpected exception; its reason reaches the Forget log and Player.log
            try
            {
                // 配送の走行と同時に箱を読まない。大きなzipはワーカーで書く
                // Never read boxes alongside an upload run; large zips are written on a worker
                await PlaytestOutboxAccess.Gate.WaitAsync(Application.exitCancellationToken);
                PlaytestExportStatus packed;
                try
                {
                    await UniTask.SwitchToThreadPool();
                    packed = PackUnsent(scope, zipPath, exportedAt);
                    await UniTask.SwitchToMainThread();
                }
                finally { PlaytestOutboxAccess.Gate.Release(); }
                FinishExport(packed, zipPath);
            }
            finally
            {
                FailIfStillRunning();
            }
        }

        // 二度押しと書き出し先の欠如をここで止め、書き出し中へ進める。進めないときはnull
        // Stops a second press and a missing destination here, then enters running; null when it cannot start
        internal string BeginExport()
        {
            if (PlaytestExportStatusBoard.Current.Value is PlaytestExportRunning)
            {
                Debug.LogWarning("[PlaytestReceiver] export already running; ignoring the second request");
                return null;
            }
            var directory = _destination.ResolveDirectory();
            if (directory == null)
            {
                Fail("desktop folder unavailable");
                return null;
            }
            PlaytestExportStatusBoard.Set(PlaytestExportRunning.Instance);
            return Path.Combine(directory, $"moorestech-reports-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        }

        // zipを書く。zip自体を書けなければ途中のzipを消して失敗を返す。どのスレッドからでも呼べる
        // Writes the zip; when the zip itself cannot be written, removes the partial file and returns failure; callable from any thread
        internal PlaytestExportStatus PackUnsent(PlaytestOutboxScope scope, string zipPath, DateTime exportedAtUtc)
        {
            // 外部ディスクIO境界。容量不足・権限でzip自体を書けなければ失敗にする
            // External disk IO boundary: failing to write the zip itself (disk full, permissions) is an export failure
            try
            {
                var result = PlaytestUnsentZipWriter.Write(_directories, scope, zipPath, exportedAtUtc);
                return new PlaytestExportDone(zipPath, result.BoxCount, result.Missing, false);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                DeletePartial(zipPath);
                return new PlaytestExportFailed($"{error.GetType().Name}: {error.Message}");
            }
        }

        // 結果を画面へ出す。成功ならファイルマネージャでzipを選んで開く。メインスレッドで呼ぶ
        // Publishes the outcome; on success opens the file manager with the zip selected; call on the main thread
        internal void FinishExport(PlaytestExportStatus packed, string zipPath)
        {
            if (packed is PlaytestExportFailed failed)
            {
                Fail(failed.Reason);
                return;
            }
            var done = (PlaytestExportDone)packed;
            var revealed = _reveal.Reveal(zipPath);
            Debug.Log($"[PlaytestReceiver] exported {done.BoxCount} box(es) to {zipPath}; {done.Missing.Count} item(s) missing");
            PlaytestExportStatusBoard.Set(new PlaytestExportDone(zipPath, done.BoxCount, done.Missing, revealed));
        }

        internal static void FailIfStillRunning()
        {
            if (PlaytestExportStatusBoard.Current.Value is PlaytestExportRunning) Fail("export ended unexpectedly; see Player.log");
        }

        private static void DeletePartial(string zipPath)
        {
            // 外部ディスクIO境界。消せない途中のzipはログに残し、画面は失敗のまま出す
            // External disk IO boundary: a partial zip that cannot be removed is logged; the screen still shows the failure
            try { if (File.Exists(zipPath)) File.Delete(zipPath); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[PlaytestReceiver] cannot remove partial export {zipPath}: {error.Message}");
            }
        }

        private static void Fail(string reason)
        {
            Debug.LogWarning($"[PlaytestReceiver] export failed: {reason}");
            PlaytestExportStatusBoard.Set(new PlaytestExportFailed(reason));
        }
    }
}
