using System.Threading;
using Client.Skit.Skit;
using Cysharp.Threading.Tasks;

namespace Client.Skit.UI
{
    public interface ISkitAutoAdvanceSink
    {
        void CompleteAutoAdvance();
    }

    public class SkitIntentWaitController
    {
        private const int AutoAdvanceDelayMs = 1000;
        private UniTaskCompletionSource _advanceWait;
        private UniTaskCompletionSource<string> _selectionWait;
        private CancellationTokenSource _autoAdvanceCancellation;
        private ISkitActionController _actionController;
        private ISkitAutoAdvanceSink _autoAdvanceSink;

        public void Bind(ISkitActionController actionController, ISkitAutoAdvanceSink autoAdvanceSink)
        {
            _actionController = actionController;
            _autoAdvanceSink = autoAdvanceSink;
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

        public void CompleteAdvance()
        {
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
                _autoAdvanceSink.CompleteAutoAdvance();
        }

        private void CancelAutoAdvanceTimer()
        {
            _autoAdvanceCancellation?.Cancel();
            _autoAdvanceCancellation?.Dispose();
            _autoAdvanceCancellation = null;
        }
    }
}
