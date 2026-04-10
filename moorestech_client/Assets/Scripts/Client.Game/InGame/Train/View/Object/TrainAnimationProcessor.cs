using System;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    public sealed class TrainAnimationProcessor : MonoBehaviour, ITrainCarObjectProcessor
    {
        private const double AnimationSpeed = 0.0025;

        private Animator[] _animators;
        private float[] _animatorBaseSpeeds;
        private bool _isReady;

        public void Initialize(TrainCarEntityObject trainCarEntityObject)
        {
            // Animator 一覧と基準速度キャッシュを初期化する
            // Initialize Animator list and authored-speed cache
            _animators = GetComponentsInChildren<Animator>(true);
            _animatorBaseSpeeds = new float[_animators.Length];
            _isReady = true;
        }

        public void Update(TrainCarContext context)
        {
            // 初期化完了後だけアニメ速度を更新する
            // Update animation speed only after initialization completes
            if (!_isReady)
            {
                return;
            }

            // snapshot が無い時は停止速度を使う
            // Use zero playback when snapshot is unavailable
            var currentSpeed = context.HasSnapshot ? context.CurrentSpeed : 0.0;
            ApplyAnimationSpeed(currentSpeed);
            
            #region Internal
            
            void ApplyAnimationSpeed(double currentSpeed)
            {
                // 走行速度に応じた再生速度を Animator に適用する
                // Apply playback speed synchronized to train movement
                var playbackSpeed = (float)(Math.Abs(currentSpeed) * AnimationSpeed);
                for (var i = 0; i < _animators.Length; i++)
                {
                    var animator = _animators[i];
                    var baseSpeed = ResolveAnimatorBaseSpeed(i, animator);
                    animator.speed = playbackSpeed / baseSpeed;
                }
            }
            
            float ResolveAnimatorBaseSpeed(int index, Animator animator)
            {
                // 一度求めた基準速度は再利用する
                // Reuse the resolved authored speed once captured
                if (_animatorBaseSpeeds[index] > 0f)
                {
                    return _animatorBaseSpeeds[index];
                }
                
                // Animator.speed を除いた state 側の速度を復元する
                // Strip Animator.speed to recover controller-authored speed
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
}
