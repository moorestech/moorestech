using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    // バグ報告画面からの再送・書き出しの入口。outboxへの書き込みは配送と同じ門で直列化する
    // Entry point for resend and export from the bug-report page; outbox writes share the delivery gate
    public sealed class PlaytestOutboxRecovery
    {
        private readonly IPlaytestUploadRequester _uploadRequester;
        private readonly PlaytestOutboxDirectories _directories;

        public PlaytestOutboxRecovery(IPlaytestUploadRequester uploadRequester, PlaytestOutboxDirectories directories)
        {
            _uploadRequester = uploadRequester;
            _directories = directories;
        }

        public void RequestResendFailed()
        {
            ResendFailedAsync(PlaytestDeliveryRouteResolver.ResolveCurrent().Scope).Forget();
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
    }
}
