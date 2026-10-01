using Client.Game.InGame.Player.FlyMode;
using NUnit.Framework;

namespace Client.Tests.Player
{
    /// <summary>
    ///     連打の間隔境界(0.3秒)を確認する
    ///     Verifies the 0.3 second tap interval boundary
    /// </summary>
    public class SpaceTapSequenceTest
    {
        // 0.3秒ちょうどはfloat減算の誤差で開始時刻により揺れる。実入力はフレーム刻みで一致しないため境界の両側で固定する
        // Exactly 0.3s wobbles with float subtraction error; real input is frame-quantized, so pin both sides of the boundary instead
        [Test]
        public void 間隔029秒は連続として数える()
        {
            var sequence = new SpaceTapSequence();

            Assert.AreEqual(1, sequence.RegisterTap(1.0f));
            Assert.AreEqual(2, sequence.RegisterTap(1.29f));
            Assert.AreEqual(3, sequence.RegisterTap(1.58f));
        }

        [Test]
        public void 間隔031秒は1から数え直す()
        {
            var sequence = new SpaceTapSequence();

            Assert.AreEqual(1, sequence.RegisterTap(1.0f));
            Assert.AreEqual(2, sequence.RegisterTap(1.29f));
            Assert.AreEqual(1, sequence.RegisterTap(1.60f));
        }

        [Test]
        public void Reset後の押下は間隔内でも1から数える()
        {
            var sequence = new SpaceTapSequence();
            sequence.RegisterTap(1.0f);

            sequence.Reset();

            Assert.AreEqual(1, sequence.RegisterTap(1.1f));
        }
    }
}
