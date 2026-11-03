using Game.Block.Blocks.CleanRoom.Machine.Yield;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class ChipYieldDrawTest
    {
        // Tier2相当の良品1個あたりのレベル別割合と失敗ダイ割合
        // Tier-2-like per-survivor level rates and failed-die rate
        private static readonly double[] TierTwoLevelRates = { 0.1, 0.7, 0.1 };
        private const double TierTwoFailedRate = 0.1;

        [Test]
        public void SameSeedReproducesEveryDrawTest()
        {
            // 同じシードからは層判定・ダイ振り分けまで同じ結果になる
            // The same seed reproduces the layer roll and the die split
            var first = new CleanRoomDrawRandom(123456789L);
            var second = new CleanRoomDrawRandom(123456789L);
            Assert.AreEqual(ChipYieldDraw.DrawLayerFailure(0.3, first), ChipYieldDraw.DrawLayerFailure(0.3, second));
            var firstDicing = ChipYieldDraw.DrawDicing(300, 0.2, TierTwoLevelRates, TierTwoFailedRate, first);
            var secondDicing = ChipYieldDraw.DrawDicing(300, 0.2, TierTwoLevelRates, TierTwoFailedRate, second);
            CollectionAssert.AreEqual(firstDicing.GoodDieCountsByLevel, secondDicing.GoodDieCountsByLevel);
            Assert.AreEqual(firstDicing.FailedDieCount, secondDicing.FailedDieCount);
        }

        [Test]
        public void LayerFailureFrequencyMatchesRateTest()
        {
            const int drawCount = 20000;
            var failures = 0;
            var random = new CleanRoomDrawRandom(7L);
            for (var i = 0; i < drawCount; i++)
            {
                if (ChipYieldDraw.DrawLayerFailure(0.25, random)) failures++;
            }
            Assert.AreEqual(0.25, failures / (double)drawCount, 0.02);
            Assert.IsFalse(ChipYieldDraw.DrawLayerFailure(0.0, new CleanRoomDrawRandom(1L)));
        }

        [Test]
        public void DicingConservesDieCountTest()
        {
            // 良品の各レベルと失敗ダイ（損失＋失敗枠）の合計は常に1枚あたりのダイ数に一致する
            // Good dies across levels plus failed dies (losses + failure slot) always equal the dies per wafer
            for (var seed = 0L; seed < 100; seed++)
            {
                var result = ChipYieldDraw.DrawDicing(300, 0.3, TierTwoLevelRates, TierTwoFailedRate, new CleanRoomDrawRandom(seed));
                Assert.AreEqual(3, result.GoodDieCountsByLevel.Count);
                var total = result.FailedDieCount;
                foreach (var count in result.GoodDieCountsByLevel) total += count;
                Assert.AreEqual(300, total);
            }
        }

        [Test]
        public void DicingMeanMatchesExpectationTest()
        {
            // 損失0.1: Lv2の平均=300×0.9×0.7=189、失敗ダイの平均=300×0.1+300×0.9×0.1=57（2000枚の標準誤差は約0.17）
            // Loss 0.1: Lv2 mean = 300x0.9x0.7 = 189, failed mean = 300x0.1 + 300x0.9x0.1 = 57 (standard error ~0.17 over 2000 wafers)
            const int waferCount = 2000;
            var levelTwoTotal = 0L;
            var levelThreeTotal = 0L;
            var failedTotal = 0L;
            var random = new CleanRoomDrawRandom(2026L);
            for (var i = 0; i < waferCount; i++)
            {
                var result = ChipYieldDraw.DrawDicing(300, 0.1, TierTwoLevelRates, TierTwoFailedRate, random);
                levelTwoTotal += result.GoodDieCountsByLevel[1];
                levelThreeTotal += result.GoodDieCountsByLevel[2];
                failedTotal += result.FailedDieCount;
            }
            Assert.AreEqual(189.0, levelTwoTotal / (double)waferCount, 1.0);
            Assert.AreEqual(27.0, levelThreeTotal / (double)waferCount, 1.0);
            Assert.AreEqual(57.0, failedTotal / (double)waferCount, 1.0);
        }

        [Test]
        public void QualityGateNeverDrawsAboveRareTest()
        {
            // Tier1の割合（Lv1とレアLv2だけ）ではLv3以上が常に0
            // Tier 1 rates (only Lv1 and the rare Lv2) never draw Lv3 or above
            var tierOneLevelRates = new[] { 0.7, 0.1, 0.0, 0.0, 0.0 };
            for (var seed = 0L; seed < 200; seed++)
            {
                var result = ChipYieldDraw.DrawDicing(300, 0.15, tierOneLevelRates, 0.2, new CleanRoomDrawRandom(seed));
                for (var levelIndex = 2; levelIndex < 5; levelIndex++) Assert.AreEqual(0, result.GoodDieCountsByLevel[levelIndex], $"seed {seed} Lv{levelIndex + 1}");
            }
        }
    }
}
