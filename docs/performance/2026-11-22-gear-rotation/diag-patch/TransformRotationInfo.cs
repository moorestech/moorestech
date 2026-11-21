using System;
using Game.Gear.Common;
using UnityEngine;
using Unity.Profiling;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [Serializable]
    [SubclassSelectorName("Transform Rotate")]
    public class TransformRotationInfo : RotationInfo
    {
        [SerializeField] private RotationAxis rotationAxis;
        [SerializeField] private Transform rotationTransform;
        [SerializeField] private float rotationSpeed = 1;

        // 軸のワールド符号。ブロックは向きを決めて生成され以後回らず、回転パーツの入れ子も無いので、初回に1度だけ求める
        // World sign of the axis; blocks spawn with their final yaw and nested rotating parts do not exist, so it is computed once
        [NonSerialized] private bool _hasWorldAxisSign;
        [NonSerialized] private float _worldAxisSign;

        public override Transform RotationTransform => rotationTransform;
        private static readonly ProfilerMarker DiagRotate = new(ProfilerCategory.Scripts, "GearDiag.TransformRotate");

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

            // 停止中やdeltaTime=0では書かない。Transformの変更通知が描画側の更新を起こすため
            // Skip writing when stopped or deltaTime is 0; a Transform change triggers renderer-side updates
            if (angle == 0f) return; // silent-ok: 回転角0は書いても見た目が変わらない / RPM0の歯車が止まって見えることで確認する
            angle *= CalculateDirectionSign(gearStateDetail.IsClockwise);

            using (DiagRotate.Auto()) rotationTransform.Rotate(GearWorldRotationSign.ToAxisVector(rotationAxis) * angle);
        }

        private float CalculateDirectionSign(bool isClockwise)
        {
            // 方向固定パーツはネットワーク符号もワールド符号も無視して常に正転
            // Direction-fixed parts always run forward, ignoring both network and world signs
            if (directionMode == GearRotationDirectionMode.AlwaysForward) return 1f;

            // ワールド符号規約: 軸のワールド正方向から見た回転方向を全設置方向で一致させる
            // World-sign convention: keep the apparent spin viewed from the positive world axis consistent across directions
            if (!_hasWorldAxisSign)
            {
                _worldAxisSign = GearWorldRotationSign.GetWorldAxisSign(rotationTransform.rotation, rotationAxis);
                _hasWorldAxisSign = true;
            }
            var networkSign = isClockwise ? 1f : -1f;
            return _worldAxisSign * networkSign;
        }
    }
}
