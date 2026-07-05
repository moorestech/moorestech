using System;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
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

        public TransformRotationInfo()
        {
        }

        // テスト用コンストラクタ
        // Constructor for tests
        public TransformRotationInfo(RotationAxis axis, Transform transform, float speed, bool reverse, GearRotationDirectionMode mode)
        {
            rotationAxis = axis;
            rotationTransform = transform;
            rotationSpeed = speed;
            isReverse = reverse;
            directionMode = mode;
        }

        public override void Rotate(GearStateDetail gearStateDetail, float deltaTime)
        {
            if (rotationTransform == null) return;

            // RPMからこのフレームの回転角を計算
            // Compute this frame's rotation angle from RPM
            var angle = gearStateDetail.CurrentRpm / 60 * deltaTime * 360 * rotationSpeed;
            angle *= isReverse ? -1 : 1;
            angle *= gearStateDetail.IsClockwise ? 1 : -1;

            rotationTransform.Rotate(GearWorldRotationSign.ToAxisVector(rotationAxis) * angle);
        }
    }
}
