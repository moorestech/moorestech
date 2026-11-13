using System;
using UniRx;

namespace Client.Ui.Foundation.Motion.Core
{
    /// <summary>
    /// 注入時計で 1 つの動きを進める走行器
    /// Runner advancing one animation by an injected clock
    /// </summary>
    /// <remarks>
    /// 取消は通知しない。完了は 1 回だけ通知する
    /// Cancel is silent; completion notifies once
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
