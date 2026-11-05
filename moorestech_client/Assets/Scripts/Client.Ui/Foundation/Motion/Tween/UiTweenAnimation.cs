using Client.Ui.Foundation.Motion.Core;
using UnityEngine;

namespace Client.Ui.Foundation.Motion.Tween
{
    /// <summary>
    /// 尺・遅延・曲線・繰り返し方に従って進み具合を Step へ書く tween
    /// Tween that writes progress to a step following duration, delay, easing and loop
    /// </summary>
    public sealed class UiTweenAnimation : IUiAnimation
    {
        private readonly UiMotionSpec _spec;
        private readonly IUiTweenStep _step;

        public UiTweenAnimation(UiMotionSpec spec, IUiTweenStep step)
        {
            _spec = spec;
            _step = step;
        }

        public void Begin()
        {
            // 経過時間だけで状態が決まるので戻す内部状態は無い
            // State depends only on elapsed time, so there is nothing internal to rewind
        }

        public bool Advance(float elapsedMilliseconds)
        {
            // 予約の遅延中は何も書かない
            // Write nothing while the scheduled delay runs
            var active = elapsedMilliseconds - _spec.DelayMilliseconds;
            if (active < 0f) return false;

            // Once は尺で止めて終わりを知らせる
            // Once clamps at the duration and reports the end
            var cycles = _spec.DurationMilliseconds <= 0f ? 1f : active / _spec.DurationMilliseconds;
            if (_spec.Loop == UiMotionLoop.Once)
            {
                _step.Apply(UiEasingFunctions.Evaluate(_spec.Easing, Mathf.Min(cycles, 1f)));
                return 1f <= cycles;
            }

            // Repeat は周の端数、PingPong は偶数周で往路・奇数周で復路
            // Repeat uses the cycle fraction; PingPong goes forward on even cycles and back on odd ones
            var fraction = cycles - Mathf.Floor(cycles);
            var isReturnLeg = _spec.Loop == UiMotionLoop.PingPong && ((int)Mathf.Floor(cycles) & 1) == 1;
            _step.Apply(UiEasingFunctions.Evaluate(_spec.Easing, isReturnLeg ? 1f - fraction : fraction));
            return false;
        }
    }
}
