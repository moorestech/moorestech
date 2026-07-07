using Client.Game.InGame.BlockSystem.StateProcessor;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class GearWorldRotationSignTest
    {
        [Test]
        public void 無回転のZ軸は正符号()
        {
            Assert.AreEqual(1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.identity, RotationAxis.Z));
        }

        [Test]
        public void Yaw180のZ軸は負符号()
        {
            // 南向き配置相当。Z軸がワールド-Zを向くため符号が反転する
            // South-facing placement; local Z points to world -Z, so the sign flips
            Assert.AreEqual(-1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, 180, 0), RotationAxis.Z));
        }

        [Test]
        public void Yaw90のZ軸は正符号()
        {
            // Z軸がワールド+Xを向く。支配成分Xが正なので正符号
            // Local Z points to world +X; dominant component X is positive
            Assert.AreEqual(1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, 90, 0), RotationAxis.Z));
        }

        [Test]
        public void Yaw270のZ軸は負符号()
        {
            Assert.AreEqual(-1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, 270, 0), RotationAxis.Z));
        }

        [Test]
        public void 任意YawのY軸は常に正符号()
        {
            // 垂直軸はYaw回転の影響を受けない（平置き歯車は元々バグの影響外）
            // Vertical axis is unaffected by yaw; flat gears were never affected by the bug
            foreach (var yaw in new[] { 0f, 90f, 180f, 270f })
                Assert.AreEqual(1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, yaw, 0), RotationAxis.Y), $"yaw={yaw}");
        }

        [Test]
        public void Yaw180のX軸は負符号()
        {
            Assert.AreEqual(-1f, GearWorldRotationSign.GetWorldAxisSign(Quaternion.Euler(0, 180, 0), RotationAxis.X));
        }

        [Test]
        public void ToAxisVectorが各軸の単位ベクトルを返す()
        {
            Assert.AreEqual(Vector3.right, GearWorldRotationSign.ToAxisVector(RotationAxis.X));
            Assert.AreEqual(Vector3.up, GearWorldRotationSign.ToAxisVector(RotationAxis.Y));
            Assert.AreEqual(Vector3.forward, GearWorldRotationSign.ToAxisVector(RotationAxis.Z));
        }
    }
}
