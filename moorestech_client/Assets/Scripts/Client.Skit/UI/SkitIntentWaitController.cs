using System.Threading;
using Client.Skit.Skit;
using Cysharp.Threading.Tasks;

namespace Client.Skit.UI
{
    // 送り待ちを解く直前に版を進める口。手動の送りも自動送りも同じ順で通る
    // Hook that advances the revision right before an advance wait is released; manual and auto advance share it
    public interface ISkitAdvanceReleaseSink
    {
        void AdvanceRevisionBeforeRelease();
    }

    public class SkitIntentWaitController
    {
        private const int AutoAdvanceDelayMs = 1000;
        private UniTaskCompletionSource _advanceWait;
        private UniTaskCompletionSource<string> _selectionWait;
        private CancellationTokenSource _autoAdvanceCancellation;
        private ISkitActionController _actionController;
        private ISkitAdvanceReleaseSink _releaseSink;

        public void Bind(ISkitActionController actionController, ISkitAdvanceReleaseSink releaseSink)
        {
            _actionController = actionController;
            _releaseSink = releaseSink;
        }

        public void StartAdvanceWait()
        {
            Cancel();
            _advanceWait = new UniTaskCompletionSource();
        }

        public void StartSelectionWait()
        {
            Cancel();
            _selectionWait = new UniTaskCompletionSource<string>();
        }

        public UniTask WaitForAdvanceAsync()
        {
            return _advanceWait.Task;
        }

        public UniTask<string> WaitForSelectionAsync()
        {
            return _selectionWait.Task;
        }

        public bool IsWaitingForAdvance()
        {
            return _advanceWait != null;
        }

        // 版を進めてから待ちを解き、同じActionの再送をstaleにする
        // Advance the revision before releasing the wait so a replayed Action becomes stale
        public void CompleteAdvance()
        {
            _releaseSink.AdvanceRevisionBeforeRelease();
            var wait = _advanceWait;
            _advanceWait = null;
            CancelAutoAdvanceTimer();
            wait.TrySetResult();
        }

        public void CompleteSelection(string choiceId)
        {
            var wait = _selectionWait;
            _selectionWait = null;
            wait.TrySetResult(choiceId);
        }

        public void ResetAutoAdvanceTimer()
        {
            CancelAutoAdvanceTimer();
            if (_advanceWait == null || !_actionController.IsAuto) return;

            // autoの有効化ごとに単一タイマーを再作成する
            // Recreate exactly one timer whenever auto mode is enabled
            _autoAdvanceCancellation = new CancellationTokenSource();
            AdvanceAfterDelayAsync(_autoAdvanceCancellation.Token).Forget();
        }

        public void Cancel()
        {
            CancelAutoAdvanceTimer();
            _advanceWait?.TrySetCanceled();
            _selectionWait?.TrySetCanceled();
            _advanceWait = null;
            _selectionWait = null;
        }

        private async UniTaskVoid AdvanceAfterDelayAsync(CancellationToken token)
        {
            var canceled = await UniTask.Delay(AutoAdvanceDelayMs, cancellationToken: token)
                .SuppressCancellationThrow();
            if (!canceled && _advanceWait != null && _actionController.IsAuto)
                CompleteAdvance();
        }

        private void CancelAutoAdvanceTimer()
        {
            _autoAdvanceCancellation?.Cancel();
            _autoAdvanceCancellation?.Dispose();
            _autoAdvanceCancellation = null;
        }
    }
}
