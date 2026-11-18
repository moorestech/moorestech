using System;
using Client.Ui.Foundation.Motion.Core;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Motion.Playback
{
    /// <summary>
    /// パネルで回っている 1 つの動きの取り消し・完了の通知
    /// Cancel and completion notification of one animation running on a panel
    /// </summary>
    /// <remarks>
    /// 開始は次のスケジューラ呼び出しで行い、その時刻を頭にする（パネルの時刻は呼び出しの中でしか読めないため）。停止は Stop 1 か所に集め、取り消し・完了・パネルから外れたの 3 経路がそれを呼ぶ。止まった動きは再生し直せない（呼び手が最終の姿を書いて捨てる）
    /// Start happens on the next scheduler callback and uses its time as the head, since panel time is only readable inside callbacks; stopping is gathered in Stop, called by cancel, completion and detach; a stopped motion cannot be replayed (the caller writes the final pose and drops it)
    /// </remarks>
    public sealed class UiMotionHandle
    {
        public IObservable<Unit> Completed => _timeline.Completed;

        private readonly UiMotionTimeline _timeline;
        private IVisualElementScheduledItem _scheduledItem;
        private readonly EventCallback<DetachFromPanelEvent> _onDetach;
        private VisualElement _target;
        private bool _isStartPending = true;

        public UiMotionHandle(UiMotionTimeline timeline)
        {
            _timeline = timeline;
            _onDetach = _ => Stop();
        }

        internal void Initialize(VisualElement target, IVisualElementScheduledItem scheduledItem)
        {
            _target = target;
            _scheduledItem = scheduledItem;
            _target.RegisterCallback(_onDetach);
        }

        public void Cancel()
        {
            Stop();
        }

        internal void AdvanceFrame()
        {
            // 予約された開始をこの呼び出しの時刻で行う
            // Perform the pending start at this callback's time
            if (_isStartPending)
            {
                _isStartPending = false;
                _timeline.Start();
            }

            // 完了したら止める
            // Stop once finished
            if (_timeline.Tick()) Stop();
        }

        private void Stop()
        {
            _target.UnregisterCallback(_onDetach);
            _timeline.Cancel();
            _isStartPending = false;
            _scheduledItem.Pause();
        }
    }
}
