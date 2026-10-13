using System.Threading;
using Client.Starter.Playtest.TitleGates;
using Cysharp.Threading.Tasks;

namespace Client.Starter.EventMode
{
    internal static class EventModeAutoStartSequence
    {
        internal static async UniTask RunAsync(IEventModeAutoStartActions actions,
            float deadlineSeconds, CancellationToken exitToken)
        {
            // 既存ゲートの期限付き待機を通し、期限後の開始は行わない
            // Use the existing bounded wait and never start after expiry
            var passed = await PlaytestTitleGates.WaitUntilPassedWithinDeadlineAsync(deadlineSeconds, exitToken);

            // 終了要求が来ていれば、開始も期限切れ終了も重ねない
            // Do not start or issue another expiry quit after exit was requested
            exitToken.ThrowIfCancellationRequested();
            if (!passed)
            {
                actions.QuitBecauseTitleGatesTimedOut(deadlineSeconds);
                return;
            }
            actions.StartFreshWorld();
        }
    }
}
