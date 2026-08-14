using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.Common
{
    public class ScrollStepAccumulatorTest
    {
        // ホイール1ノッチ相当の入力量。InputSystemの120を100で割った値
        // One wheel notch of input: the Input System's 120 divided by 100
        private const float OneNotch = 1.2f;

        [Test]
        public void 順方向1ノッチで1段進む()
        {
            var accumulator = new ScrollStepAccumulator();
            Assert.AreEqual(1, accumulator.Accumulate(OneNotch));
        }

        [Test]
        public void 順方向の端数が残っていても逆方向1ノッチで1段戻る()
        {
            // 順方向2回で端数0.4が残る状態を作り、その直後の逆回しが0段に潰れないことを確認する
            // Build up a 0.4 forward remainder, then verify the immediate reverse notch does not collapse to zero steps
            var accumulator = new ScrollStepAccumulator();
            accumulator.Accumulate(OneNotch);
            accumulator.Accumulate(OneNotch);

            Assert.AreEqual(-1, accumulator.Accumulate(-OneNotch));
        }

        [Test]
        public void 逆方向の端数が残っていても順方向1ノッチで1段進む()
        {
            var accumulator = new ScrollStepAccumulator();
            accumulator.Accumulate(-OneNotch);
            accumulator.Accumulate(-OneNotch);

            Assert.AreEqual(1, accumulator.Accumulate(OneNotch));
        }

        [Test]
        public void ノッチ未満の微小デルタは蓄積されてから1段になる()
        {
            // トラックパッドの細かいデルタを取りこぼさないことを確認する
            // Verify fine trackpad deltas are not dropped
            var accumulator = new ScrollStepAccumulator();
            Assert.AreEqual(0, accumulator.Accumulate(0.4f));
            Assert.AreEqual(0, accumulator.Accumulate(0.4f));
            Assert.AreEqual(1, accumulator.Accumulate(0.4f));
        }

        [Test]
        public void Resetで端数が捨てられる()
        {
            var accumulator = new ScrollStepAccumulator();
            accumulator.Accumulate(0.9f);
            accumulator.Reset();

            Assert.AreEqual(0, accumulator.Accumulate(0.9f));
        }
    }
}
