using System;
using Client.Ui.Foundation.Motion.Core;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Motion.Playback
{
    /// <summary>
    /// パネルで回っている 1 つの動きの再生し直し・取り消し・完了の通知
    /// Restart, cancel and completion notification of one animation running on a panel
    /// </summary>
    /// <remarks>
    /// 開始と再生し直しは次のスケジューラ呼び出しで行い、その時刻を頭にする（パネルの時刻は呼び出しの中でしか読めないため）。停止は Stop 1 か所に集め、取り消し・完了・パネルから外れたの 3 経路がそれを呼ぶ
    /// Start and restart happen on the next scheduler callback and use its time as the head, since panel time is only readable inside callbacks; stopping is gathered in Stop, called by cancel, completion and detach
    /// </remarks>
    public sealed class UiMotionHandle
    {
        public IObservable<Unit> Completed => _timeline.Completed;

        private readonly UiMotionTimeline _timeline;
        private IVisualElementScheduledItem _scheduledItem;
        private bool _isStartPending = true;

        public UiMotionHandle(UiMotionTimeline timeline)
        {
            _timeline = timeline;
        }

        internal void Initialize(VisualElement target, IVisualElementScheduledItem scheduledItem)
        {
            _scheduledItem = scheduledItem;
            target.RegisterCallback<DetachFromPanelEvent>(_ => Stop());
        }

        public void Restart()
        {
            _isStartPending = true;
            _scheduledItem.Resume();
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

            // 完了の通知の中で再生し直されたら止めない
            // Do not stop if a completion subscriber restarted the motion
            if (_timeline.Tick() && !_isStartPending) Stop();
        }

        private void Stop()
        {
            _timeline.Cancel();
            _isStartPending = false;
            _scheduledItem.Pause();
        }
    }
}
