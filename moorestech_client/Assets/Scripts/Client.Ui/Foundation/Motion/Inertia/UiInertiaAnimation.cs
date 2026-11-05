using Client.Ui.Foundation.Motion.Core;
using UnityEngine;

namespace Client.Ui.Foundation.Motion.Inertia
{
    /// <summary>
    /// 指数で減衰する速度の慣性。速度が止まりの閾値を下回ったら終わる
    /// Inertia whose velocity decays exponentially; ends once the speed drops below the stop threshold
    /// </summary>
    /// <remarks>
    /// 速度 v(t) = v0·exp(-k t)、変位 v0·(1 - exp(-k t)) / k。閉じた式なのでフレームの粗さに依らない
    /// Velocity v(t) = v0·exp(-k t), displacement v0·(1 - exp(-k t)) / k; closed form, so frame granularity does not matter
    /// </remarks>
    public sealed class UiInertiaAnimation : IUiAnimation
    {
        private readonly float _initialVelocityPerMillisecond;
        private readonly float _decayPerMillisecond;
        private readonly float _stopVelocityPerMillisecond;
        private readonly IUiInertiaStep _step;

        public UiInertiaAnimation(float initialVelocityPerMillisecond, float decayPerMillisecond, float stopVelocityPerMillisecond, IUiInertiaStep step)
        {
            _initialVelocityPerMillisecond = initialVelocityPerMillisecond;
            _decayPerMillisecond = decayPerMillisecond;
            _stopVelocityPerMillisecond = stopVelocityPerMillisecond;
            _step = step;
        }

        public void Begin()
        {
            // 経過時間だけで状態が決まるので戻す内部状態は無い
            // State depends only on elapsed time, so there is nothing internal to rewind
        }

        public bool Advance(float elapsedMilliseconds)
        {
            // 経過時間の減衰率から変位と速度を出す
            // Derive displacement and velocity from the decay over elapsed time
            var decay = Mathf.Exp(-_decayPerMillisecond * elapsedMilliseconds);
            _step.Apply(_initialVelocityPerMillisecond * (1f - decay) / _decayPerMillisecond);
            return Mathf.Abs(_initialVelocityPerMillisecond * decay) < _stopVelocityPerMillisecond;
        }
    }
}
