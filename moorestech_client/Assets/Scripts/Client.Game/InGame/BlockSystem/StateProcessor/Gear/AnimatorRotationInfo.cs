using System;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [Serializable]
    [SubclassSelectorName("Animator")]
    public class AnimatorRotationInfo : RotationInfo
    {
        // 逆再生用のSpeed Multiplierパラメータ名。AnimatorController側でfloat(既定値1)を宣言しステートのSpeed Multiplierに割り当てる
        // Speed Multiplier parameter name for reverse playback; declare a float (default 1) on the controller and bind it to the state's speed multiplier
        public const string DirectionParameterName = "GearRotationDirection";

        [SerializeField] private Animator animator;
        [SerializeField] private float rpm60Speed = 1;
        [SerializeField] private AnimationPlayDirection defaultPlayDirection = AnimationPlayDirection.Positive;

        public Animator Animator => animator;
        public float Rpm60Speed => rpm60Speed;

        private bool _directionParameterChecked;
        private bool _hasDirectionParameter;

        public override void Rotate(GearStateDetail gearStateDetail, float deltaTime)
        {
            if (animator == null) return;

            // 速度の大きさはAnimator.speed、方向はSpeed Multiplierパラメータに分離する(負のAnimator.speedは非サポートのため)
            // Split magnitude into Animator.speed and direction into the multiplier parameter (negative Animator.speed is unsupported)
            var rpmRate = gearStateDetail.CurrentRpm / 60f;
            animator.speed = Mathf.Abs(rpm60Speed * rpmRate);

            var direction = CalculateDirection(gearStateDetail.IsClockwise, directionMode, isReverse, defaultPlayDirection);
            if (HasDirectionParameter()) animator.SetFloat(DirectionParameterName, direction);
        }

        /// <summary>
        /// 再生方向(+1/-1)を計算する純関数
        /// Pure function computing the playback direction (+1/-1)
        /// </summary>
        public static float CalculateDirection(bool isClockwise, GearRotationDirectionMode mode, bool reverse, AnimationPlayDirection defaultDirection)
        {
            var networkSign = mode == GearRotationDirectionMode.AlwaysForward || isClockwise ? 1f : -1f;
            var reverseSign = reverse ? -1f : 1f;
            var defaultSign = defaultDirection == AnimationPlayDirection.Positive ? 1f : -1f;
            return networkSign * reverseSign * defaultSign;
        }

        private bool HasDirectionParameter()
        {
            // パラメータ有無を初回のみ走査してキャッシュ。無いコントローラーは正転フォールバック
            // Scan parameters once and cache; controllers without the parameter fall back to forward playback
            if (_directionParameterChecked) return _hasDirectionParameter;
            _directionParameterChecked = true;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name != DirectionParameterName || parameter.type != AnimatorControllerParameterType.Float) continue;
                _hasDirectionParameter = true;
                break;
            }
            return _hasDirectionParameter;
        }
    }
}
