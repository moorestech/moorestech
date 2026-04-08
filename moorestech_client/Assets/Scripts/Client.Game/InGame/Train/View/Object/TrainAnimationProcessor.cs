using System;
using Client.Game.InGame.Train.View;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    public class TrainAnimationProcessor : MonoBehaviour, ITrainCarObjectProcessor
    {
        // 列車速度から Animator 再生速度へ変換する係数
        // Conversion factor from train speed to Animator playback speed
        private const double AnimationSpeed = 0.0025;

        private Animator[] _animators;
        private float[] _animatorBaseSpeeds;

        public void Initialize(TrainCarEntityObject trainCarEntityObject)
        {
            // 子 Animator 一覧を初期化時に確定する
            // Capture the child Animator list during initialization
            _animators = GetComponentsInChildren<Animator>(true);
            _animatorBaseSpeeds = new float[_animators.Length];
        }

        public void Update(TrainCarContext context)
        {
            // snapshot が無い時はアニメを停止状態にする
            // Stop animation playback when snapshot is unavailable
            var playbackSpeed = context.HasSnapshot ? (float)(Math.Abs(context.CurrentSpeed) * AnimationSpeed) : 0f;
            for (var i = 0; i < _animators.Length; i++)
            {
                var animator = _animators[i];
                var baseSpeed = ResolveAnimatorBaseSpeed(i, animator);
                animator.speed = playbackSpeed / baseSpeed;
            }
        }

        #region Internal

        private float ResolveAnimatorBaseSpeed(int index, Animator animator)
        {
            // 一度解決した基礎速度は使い回す
            // Reuse the resolved base playback speed once captured
            if (_animatorBaseSpeeds[index] > 0f)
            {
                return _animatorBaseSpeeds[index];
            }

            // Animator.speed を剥がして state 側の基礎速度を求める
            // Recover the authored state speed by stripping Animator.speed
            var currentAnimatorSpeed = Mathf.Abs(animator.speed);
            if (currentAnimatorSpeed <= Mathf.Epsilon)
            {
                currentAnimatorSpeed = 1f;
            }

            var stateSpeed = animator.GetCurrentAnimatorStateInfo(0).speed;
            var resolvedBaseSpeed = stateSpeed > 0f ? stateSpeed / currentAnimatorSpeed : 1f;
            _animatorBaseSpeeds[index] = resolvedBaseSpeed;
            return resolvedBaseSpeed;
        }

        #endregion
    }
}
