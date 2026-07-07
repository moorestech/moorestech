using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class TransformRotationInfoTest
    {
        [Test]
        public void 対向配置の回転パーツが同一ワールド方向に回転する()
        {
            // 北向きと南向き(Yaw180度差)で同じ設定のパーツが同じワールド回転をすることを検証
            // Verify that identically configured parts facing north and south spin the same way in world space
            var deltaNorth = RotateOnce(0f, true, GearRotationDirectionMode.NetworkSigned);
            var deltaSouth = RotateOnce(180f, true, GearRotationDirectionMode.NetworkSigned);

            AssertSameSpin(deltaNorth, deltaSouth);
        }

        [Test]
        public void 反時計回りは時計回りと逆方向に回転する()
        {
            var clockwise = RotateOnce(0f, true, GearRotationDirectionMode.NetworkSigned);
            var counterClockwise = RotateOnce(0f, false, GearRotationDirectionMode.NetworkSigned);

            clockwise.ToAngleAxis(out var angleCw, out var axisCw);
            counterClockwise.ToAngleAxis(out var angleCcw, out var axisCcw);
            Assert.Greater(angleCw, 0.1f);
            Assert.Greater(angleCcw, 0.1f);
            Assert.Less(Vector3.Dot(axisCw, axisCcw), -0.9f);
        }

        [Test]
        public void AlwaysForwardはネットワーク回転方向を無視する()
        {
            var clockwise = RotateOnce(0f, true, GearRotationDirectionMode.AlwaysForward);
            var counterClockwise = RotateOnce(0f, false, GearRotationDirectionMode.AlwaysForward);

            AssertSameSpin(clockwise, counterClockwise);
        }

        [Test]
        public void AlwaysForwardは設置方向にも依存しない()
        {
            // ベルト表面等はブロックローカルで正転し続ける(ワールド補正もしない)
            // Belt surfaces keep running forward in block-local space (no world-sign correction either)
            var north = RotateOnceLocal(0f, GearRotationDirectionMode.AlwaysForward);
            var south = RotateOnceLocal(180f, GearRotationDirectionMode.AlwaysForward);

            AssertSameSpin(north, south);
        }

        private static void AssertSameSpin(Quaternion deltaA, Quaternion deltaB)
        {
            deltaA.ToAngleAxis(out var angleA, out var axisA);
            deltaB.ToAngleAxis(out var angleB, out var axisB);
            Assert.Greater(angleA, 0.1f);
            Assert.AreEqual(angleA, angleB, 0.01f);
            Assert.Greater(Vector3.Dot(axisA, axisB), 0.9f);
        }

        private static Quaternion RotateOnce(float yaw, bool isClockwise, GearRotationDirectionMode mode)
        {
            var (parent, child) = CreateHierarchy(yaw);
            var info = new TransformRotationInfo(RotationAxis.Z, child, 1f, false, mode);

            var before = child.rotation;
            info.Rotate(new GearStateDetail(isClockwise, 60f, 0f), 1f / 60f);
            var delta = child.rotation * Quaternion.Inverse(before);

            Object.DestroyImmediate(parent.gameObject);
            return delta;
        }

        private static Quaternion RotateOnceLocal(float yaw, GearRotationDirectionMode mode)
        {
            var (parent, child) = CreateHierarchy(yaw);
            var info = new TransformRotationInfo(RotationAxis.Z, child, 1f, false, mode);

            var before = child.localRotation;
            info.Rotate(new GearStateDetail(true, 60f, 0f), 1f / 60f);
            var delta = child.localRotation * Quaternion.Inverse(before);

            Object.DestroyImmediate(parent.gameObject);
            return delta;
        }

        private static (Transform parent, Transform child) CreateHierarchy(float yaw)
        {
            // ブロック設置と同様に親(ブロックルート)へYawを与え、回転パーツを子に置く
            // Give yaw to the parent (block root) like block placement and put the rotating part as a child
            var parent = new GameObject("BlockRoot").transform;
            parent.rotation = Quaternion.Euler(0, yaw, 0);
            var child = new GameObject("RotationPart").transform;
            child.SetParent(parent, false);
            return (parent, child);
        }
    }
}
