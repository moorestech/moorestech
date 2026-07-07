using Client.Game.InGame.BlockSystem.StateProcessor;
using NUnit.Framework;

namespace Client.Tests.Gear
{
    public class AnimatorRotationInfoDirectionTest
    {
        [TestCase(true, false, AnimationPlayDirection.Positive, 1f)]
        [TestCase(false, false, AnimationPlayDirection.Positive, -1f)]
        [TestCase(true, false, AnimationPlayDirection.Negative, -1f)]
        [TestCase(false, false, AnimationPlayDirection.Negative, 1f)]
        [TestCase(true, true, AnimationPlayDirection.Positive, -1f)]
        [TestCase(false, true, AnimationPlayDirection.Positive, 1f)]
        public void NetworkSignedはネットワーク方向とデフォルト方向とisReverseの積(bool isClockwise, bool reverse, AnimationPlayDirection defaultDirection, float expected)
        {
            var actual = AnimatorRotationInfo.CalculateDirection(isClockwise, GearRotationDirectionMode.NetworkSigned, reverse, defaultDirection);
            Assert.AreEqual(expected, actual);
        }

        [TestCase(true, AnimationPlayDirection.Positive, 1f)]
        [TestCase(false, AnimationPlayDirection.Positive, 1f)]
        [TestCase(true, AnimationPlayDirection.Negative, -1f)]
        [TestCase(false, AnimationPlayDirection.Negative, -1f)]
        public void AlwaysForwardはネットワーク方向を無視する(bool isClockwise, AnimationPlayDirection defaultDirection, float expected)
        {
            var actual = AnimatorRotationInfo.CalculateDirection(isClockwise, GearRotationDirectionMode.AlwaysForward, false, defaultDirection);
            Assert.AreEqual(expected, actual);
        }
    }
}
