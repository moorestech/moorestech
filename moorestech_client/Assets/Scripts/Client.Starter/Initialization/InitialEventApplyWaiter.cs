using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Client.Starter.Initialization
{
    // 初期イベント適用の待機境界。全対象の完了をひとつのawaitへ畳み、失敗は例外としてここへ届く
    // The wait boundary of initial event application: every target folds into one await and failures surface here as exceptions
    public static class InitialEventApplyWaiter
    {
        private const int StuckWarningSeconds = 5;

        public static async UniTask WaitAllAsync(IReadOnlyList<IInitialEventApplyWaitTarget> targets)
        {
            var waits = targets.Select(target => (target, task: target.WaitForInitialApplyAsync().Preserve())).ToList();
            var allApplied = UniTask.WhenAll(waits.Select(wait => wait.task));

            // 対象タスクはWhenAllで一度だけawaitする。警告側でも待つとUniTaskの二重await例外になる
            // Await the targets once through WhenAll; awaiting them again in the warning path throws UniTask's double-await error
            using var warningCancellation = new CancellationTokenSource();
            WarnStuckTargetsAsync(warningCancellation.Token).Forget();

            // 失敗で抜けた場合も警告を打ち切る。起動失敗の後から出る5秒警告は原因調査を誤誘導する
            // Cut the warning off on the failure path too, since a five-second warning trailing a failed startup misdirects the diagnosis
            try
            {
                await allApplied;
            }
            finally
            {
                warningCancellation.Cancel();
            }

            #region Internal

            // 5秒未完了で詰まっている対象を顕在化し、適用待機自体は継続する
            // Surface targets stuck past five seconds while continuing to wait for their application
            async UniTaskVoid WarnStuckTargetsAsync(CancellationToken cancellationToken)
            {
                // timeScale=0のEditorでも必ず発火させるためRealtimeで測る
                // Measure in realtime so the warning still fires in an Editor sitting at timeScale zero
                var canceled = await UniTask
                    .Delay(TimeSpan.FromSeconds(StuckWarningSeconds), DelayType.Realtime, PlayerLoopTiming.Update, cancellationToken)
                    .SuppressCancellationThrow();
                if (canceled) return;

                // 未完了(Pending)だけを並べる。faultedは例外として上がるので警告に載せない
                // List only Pending targets; faulted ones surface as exceptions instead
                var pending = string.Join(", ", waits.Where(wait => wait.task.Status == UniTaskStatus.Pending).Select(wait => wait.target.GetType().Name));
                if (pending.Length == 0) return;
                Debug.LogWarning($"[InitialEventApplyWaiter] 初期イベント適用が未完了のまま待機中: {pending}");
            }

            #endregion
        }
    }
}
