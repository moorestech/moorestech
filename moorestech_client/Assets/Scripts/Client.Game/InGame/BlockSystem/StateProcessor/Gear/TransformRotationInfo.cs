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

            // RPMからこのフレームの回転角を計算し、方向符号を掛ける
            // Compute this frame's rotation angle from RPM and apply the direction sign
            var angle = gearStateDetail.CurrentRpm / 60 * deltaTime * 360 * rotationSpeed;
            angle *= isReverse ? -1 : 1;
            angle *= CalculateDirectionSign(gearStateDetail.IsClockwise);

            rotationTransform.Rotate(GearWorldRotationSign.ToAxisVector(rotationAxis) * angle);
        }

        private float CalculateDirectionSign(bool isClockwise)
        {
            // 方向固定パーツはネットワーク符号もワールド符号も無視して常に正転
            // Direction-fixed parts always run forward, ignoring both network and world signs
            if (directionMode == GearRotationDirectionMode.AlwaysForward) return 1f;

            // ワールド符号規約: 軸のワールド正方向から見た回転方向を全設置方向で一致させる
            // World-sign convention: keep the apparent spin viewed from the positive world axis consistent across directions
            var worldSign = GearWorldRotationSign.GetWorldAxisSign(rotationTransform.rotation, rotationAxis);
            var networkSign = isClockwise ? 1f : -1f;
            return worldSign * networkSign;
        }
    }
}
