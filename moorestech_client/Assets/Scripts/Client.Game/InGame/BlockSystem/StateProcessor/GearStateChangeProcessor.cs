using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Game.Gear.Common;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [RequireComponent(typeof(GearStateChangeProcessorSimulator))]
    public class GearStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public IReadOnlyList<RotationInfo> RotationInfos => rotationInfos;
        [SerializeReference, SubclassSelector] private List<RotationInfo> rotationInfos = new();

        private GearStateDetail _currentGearState;

        public void Initialize(BlockGameObject blockGameObject) { }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentGearState = blockState.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
        }

        private void Update()
        {
            if (_currentGearState == null) return;

            Rotate(_currentGearState);
        }

        public void Rotate(GearStateDetail gearStateDetail)
        {
            foreach (var rotationInfo in rotationInfos)
            {
                if (rotationInfo == null) continue;
                rotationInfo.Rotate(gearStateDetail);
            }
        }

        #if UNITY_EDITOR
        public GearStateDetail DebugCurrentGearState => _currentGearState;
        #endif
    }

    [Serializable]
    public abstract class RotationInfo
    {
        [SerializeField] protected bool isReverse;

        public bool IsReverse => isReverse;

        // TransformRotationInfoのみ値を返す。Simulator互換のためベースに公開
        // Only TransformRotationInfo returns a value. Exposed on base for simulator compatibility
        public virtual Transform RotationTransform => null;

        public abstract void Rotate(GearStateDetail gearStateDetail);
    }

    [Serializable]
    [SubclassSelectorName("Transform Rotate")]
    public class TransformRotationInfo : RotationInfo
    {
        [SerializeField] private RotationAxis rotationAxis;
        [SerializeField] private Transform rotationTransform;
        [SerializeField] private float rotationSpeed = 1;

        public RotationAxis RotationAxis => rotationAxis;
        public override Transform RotationTransform => rotationTransform;
        public float RotationSpeed => rotationSpeed;

        public override void Rotate(GearStateDetail gearStateDetail)
        {
            if (rotationTransform == null) return;

            var rpm = gearStateDetail.CurrentRpm;
            var rotation = rpm / 60 * Time.deltaTime * 360;

            var rotate = rotationAxis switch
            {
                RotationAxis.X => new Vector3(rotation, 0, 0),
                RotationAxis.Y => new Vector3(0, rotation, 0),
                RotationAxis.Z => new Vector3(0, 0, rotation),
                _ => Vector3.zero,
            };
            rotate *= isReverse ? -1 : 1;
            rotate *= rotationSpeed;
            rotate *= gearStateDetail.IsClockwise ? 1 : -1;

            rotationTransform.Rotate(rotate);
        }
    }

    [Serializable]
    [SubclassSelectorName("Animator")]
    public class AnimatorRotationInfo : RotationInfo
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float rpm60Speed = 1;

        public Animator Animator => animator;
        public float Rpm60Speed => rpm60Speed;

        public override void Rotate(GearStateDetail gearStateDetail)
        {
            if (animator == null) return;

            var rpmRate = gearStateDetail.CurrentRpm / 60f;
            var speed = rpm60Speed * (isReverse ? -1 : 1) * (gearStateDetail.IsClockwise ? 1 : -1) * rpmRate;
            animator.speed = speed;
        }
    }

    public enum RotationAxis
    {
        X,
        Y,
        Z,
    }
}
