using System;
using UniRx;

namespace Client.Ui.Foundation.Motion.Core
{
    /// <summary>
    /// 注入された時計で 1 つの動きを進める純粋な走行器。開始・取り消し・完了の通知を持つ（止まった動きの再生し直しの口は持たない）
    /// Pure runner that advances one animation with an injected clock; supports start, cancel and completion notification (no port to replay a stopped animation)
    /// </summary>
    /// <remarks>
    /// 取り消しは通知しない。完了は 1 回だけ OnNext → OnCompleted する（完了後に Start し直しても通知されない）
    /// Cancel does not notify; completion emits OnNext then OnCompleted once (a Start after completion is not notified)
    /// </remarks>
    public sealed class UiMotionTimeline
    {
        public bool IsRunning { get; private set; }
        public IObservable<Unit> Completed => _completed;

        private readonly IUiMotionClock _clock;
        private readonly IUiAnimation _animation;
        private readonly Subject<Unit> _completed = new();
        private long _startMilliseconds;

        public UiMotionTimeline(IUiMotionClock clock, IUiAnimation animation)
        {
            _clock = clock;
            _animation = animation;
        }

        public void Start()
        {
            // 開始時刻を今に張り、動きを頭へ戻す
            // Pin the start time to now and rewind the animation to its head
            _startMilliseconds = _clock.GetNowMilliseconds();
            _animation.Begin();
            IsRunning = true;
        }

        public void Cancel()
        {
            IsRunning = false;
        }

        public bool Tick()
        {
            if (!IsRunning) return false;

            // 開始からの経過で状態を書き、終わったら 1 回だけ通知する
            // Write the state for the elapsed time and notify once when finished
            var elapsed = (float)(_clock.GetNowMilliseconds() - _startMilliseconds);
            if (!_animation.Advance(elapsed)) return false;

            IsRunning = false;
            _completed.OnNext(Unit.Default);
            _completed.OnCompleted();
            return true;
        }
    }
}
