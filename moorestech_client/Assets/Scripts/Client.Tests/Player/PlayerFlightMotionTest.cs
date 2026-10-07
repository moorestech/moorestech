using NUnit.Framework;
using StarterAssets;

namespace Client.Tests.Player
{
    /// <summary>
    ///     飛行速度と縦速度の規則を確認する
    ///     Verifies the flight speed and vertical velocity rules
    /// </summary>
    public class PlayerFlightMotionTest
    {
        private const float SprintSpeed = 7.5f;

        [Test]
        public void 飛行速度は走りの2倍でShift中は6倍()
        {
            var motion = new PlayerFlightMotion();

            Assert.AreEqual(15f, motion.ResolveSpeed(false, SprintSpeed), 0.0001f);
            Assert.AreEqual(45f, motion.ResolveSpeed(true, SprintSpeed), 0.0001f);
        }

        [Test]
        public void 上下入力は水平と同じ速さで縦速度になる()
        {
            var motion = new PlayerFlightMotion();
            motion.SetFlying(true);

            motion.SetVerticalInput(1f);
            Assert.AreEqual(45f, motion.ResolveVerticalVelocity(-3f, 45f, false), 0.0001f);

            motion.SetVerticalInput(-1f);
            Assert.AreEqual(-15f, motion.ResolveVerticalVelocity(-3f, 15f, false), 0.0001f);
        }

        [Test]
        public void 非飛行時は重力で積算した縦速度をそのまま返す()
        {
            var motion = new PlayerFlightMotion();
            motion.SetVerticalInput(1f);

            Assert.AreEqual(-3f, motion.ResolveVerticalVelocity(-3f, 15f, false), 0.0001f);
        }

        [Test]
        public void 移動ロック中は上下入力があっても縦速度は0()
        {
            var motion = new PlayerFlightMotion();
            motion.SetFlying(true);
            motion.SetVerticalInput(1f);

            Assert.AreEqual(0f, motion.ResolveVerticalVelocity(-3f, 15f, true), 0.0001f);
        }

        [Test]
        public void 飛行の入り直しで前回の上下入力を持ち越さない()
        {
            var motion = new PlayerFlightMotion();
            motion.SetFlying(true);
            motion.SetVerticalInput(1f);

            motion.SetFlying(false);
            motion.SetFlying(true);

            Assert.AreEqual(0f, motion.ResolveVerticalVelocity(-3f, 15f, false), 0.0001f);
        }
    }
}
