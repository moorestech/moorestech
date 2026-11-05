using Client.Ui.Foundation.Motion.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Motion.Playback
{
    /// <summary>
    /// 要素のパネルのスケジューラ（実時間）を時計にして動きを回す
    /// Runs an animation using the element's panel scheduler (real time) as the clock
    /// </summary>
    public static class UiMotionPlayer
    {
        public static UiMotionHandle Play(VisualElement target, IUiAnimation animation)
        {
            // スケジューラの時刻を時計にして毎フレーム走行器を進める
            // Advance the runner every frame with the scheduler time as the clock
            var clock = new PanelSchedulerClock();
            var handle = new UiMotionHandle(new UiMotionTimeline(clock, animation));
            var item = target.schedule.Execute(state =>
            {
                clock.SetNowMilliseconds(state.now);
                handle.AdvanceFrame();
            }).Every(0);
            handle.Initialize(target, item);

            // パネルに無い要素はスケジューラが回らないので、取り消し済みで返して開発者ログで知らせる
            // A detached element has no running scheduler, so return it cancelled and report to the developer log
            if (target.panel == null)
            {
                Debug.LogError("[UiMotionPlayer] target is not attached to a panel");
                handle.Cancel();
            }
            return handle;
        }

        /// <summary>
        /// 直近のスケジューラ呼び出しの時刻を返す時計
        /// Clock that returns the time of the latest scheduler callback
        /// </summary>
        private sealed class PanelSchedulerClock : IUiMotionClock
        {
            private long _nowMilliseconds;

            public void SetNowMilliseconds(long nowMilliseconds)
            {
                _nowMilliseconds = nowMilliseconds;
            }

            public long GetNowMilliseconds()
            {
                return _nowMilliseconds;
            }
        }
    }
}
