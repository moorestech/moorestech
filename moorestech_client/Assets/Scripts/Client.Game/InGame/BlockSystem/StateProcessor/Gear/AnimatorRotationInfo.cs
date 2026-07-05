using System;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [Serializable]
    [SubclassSelectorName("Animator")]
    public class AnimatorRotationInfo : RotationInfo
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float rpm60Speed = 1;

        public Animator Animator => animator;
        public float Rpm60Speed => rpm60Speed;

        public override void Rotate(GearStateDetail gearStateDetail, float deltaTime)
        {
            if (animator == null) return;

            var rpmRate = gearStateDetail.CurrentRpm / 60f;
            var speed = rpm60Speed * (isReverse ? -1 : 1) * (gearStateDetail.IsClockwise ? 1 : -1) * rpmRate;
            animator.speed = speed;
        }
    }
}
