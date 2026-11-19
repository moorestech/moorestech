using System.Linq;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class ChipYieldFormulaTest
    {
        // モジュール無しの表とTier1の十分入れたときの表（Lv1..Lv5）と失敗ダイ
        // The no-module table and the tier-1 full-module table (Lv1..Lv5) with the failed die
        private static readonly float[] NoModuleLevels = { 0.2f, 0f, 0f, 0f, 0f };
        private const float NoModuleFailed = 0.8f;
        private static readonly float[] Tier1Levels = { 0.83125f, 0.11875f, 0f, 0f, 0f };
        private const float Tier1Failed = 0.05f;

        [Test]
        public void ReducedRateClampsQualityReductionTest()
        {
            Assert.AreEqual(0.07, ChipYieldFormula.ReducedRate(0.10, 0.3, 0.8), 1e-9);
            Assert.AreEqual(0.02, ChipYieldFormula.ReducedRate(0.10, 2.0, 0.8), 1e-9);
            Assert.AreEqual(0.10, ChipYieldFormula.ReducedRate(0.10, 0.0, 0.8), 1e-9);
        }

        [Test]
        public void ApproachWeightIsZeroWithoutQualityAndKnownAtNineModulesTest()
        {
            Assert.AreEqual(0.0, ChipYieldFormula.ApproachWeight(0, 0.5, 0.1335), 1e-12);
            Assert.AreEqual(0.0, ChipYieldFormula.ApproachWeight(-1, 0.5, 0.1335), 1e-12);

            // クラスC・Lv1を9個: 1−exp(−0.36×0.5÷0.1335)
            // Class C with nine Lv1: 1 - exp(-0.36 x 0.5 / 0.1335)
            Assert.AreEqual(0.740322, ChipYieldFormula.ApproachWeight(0.36, 0.5, 0.1335), 1e-6);
        }

        [Test]
        public void BlendedDieRatesMixesStatesPerDieTest()
        {
            // 損失0なら表の重みは近づき度そのもの。0はモジュール無しの表、1は最高Tierの表、0.5はその真ん中
            // With no loss the table weight equals the approach weight: 0 is the no-module table, 1 the tier table, 0.5 the middle
            AssertRates(Blend(0), 0.2, 0, 0, 0, 0, 0.8);
            AssertRates(Blend(1), 0.83125, 0.11875, 0, 0, 0, 0.05);
            AssertRates(Blend(0.5), 0.515625, 0.059375, 0, 0, 0, 0.425);
        }

        [Test]
        public void DicingRatesDiscountsLossByWeightTest()
        {
            // クラスC(損失0.45)・maxLossReduction 0.8: 重み0で0.45、0.5で0.27、1で0.09
            // Class C (loss 0.45) with maxLossReduction 0.8: 0.45 at weight 0, 0.27 at 0.5, 0.09 at 1
            foreach (var (weight, expectedLoss) in new[] { (0.0, 0.45), (0.5, 0.27), (1.0, 0.09) })
            {
                ChipYieldFormula.DicingRates(NoModuleLevels, NoModuleFailed, Tier1Levels, Tier1Failed, 0.45, 0.8, weight, out var dieLossRate, out _);
                Assert.AreEqual(expectedLoss, dieLossRate, 1e-12, $"weight {weight}");
            }
        }

        [Test]
        public void DicingRatesReturnsLossAndPerSurvivorRatesTest()
        {
            // 十分入れた側の生き残りが多いほど、生き残りは最高Tierの表へ寄る（表の重み 0.5×0.91÷0.73）
            // More full-module survivors pull survivors toward the tier table (table weight 0.5 x 0.91 / 0.73)
            var rates = ChipYieldFormula.DicingRates(NoModuleLevels, NoModuleFailed, Tier1Levels, Tier1Failed, 0.45, 0.8, 0.5, out var dieLossRate, out var failedRate);
            Assert.AreEqual(0.27, dieLossRate, 1e-12);
            AssertRates(rates.Append(failedRate).ToArray(), 0.593450, 0.074015, 0, 0, 0, 0.332534);
        }

        [Test]
        public void DicingRatesKeepsSumWhenEveryDieIsLostTest()
        {
            // 損失1・割引0で全ダイが損失のときも、損失率1と生き残りの振り分けの合計1を保つ（合計はfloatの表の和の誤差1e-8程度を含む）
            // With loss 1 and no reduction every die is lost, yet the loss is one and the split sums to one (within the float tables' ~1e-8 sum error)
            var rates = ChipYieldFormula.DicingRates(NoModuleLevels, NoModuleFailed, Tier1Levels, Tier1Failed, 1.0, 0.0, 0.5, out var dieLossRate, out var failedRate);
            Assert.AreEqual(1.0, dieLossRate, 1e-12);
            Assert.AreEqual(1.0, rates.Sum() + failedRate, 1e-6);
        }

        [Test]
        public void QualityGateHoldsForEveryWeightTest()
        {
            // どの重みでも Tier t で Lv t+2 以上は0、合計は1（Tier2の表で確かめる）
            // At any weight, tier t keeps Lv t+2 and above at zero and the sum at one (checked on the tier-2 table)
            var tier2Levels = new[] { 0.025f, 0.83125f, 0.11875f, 0f, 0f };
            foreach (var weight in new[] { 0.0, 0.1, 0.5, 0.9, 1.0 })
            {
                var rates = ChipYieldFormula.DicingRates(NoModuleLevels, NoModuleFailed, tier2Levels, 0.025f, 0.45, 0.8, weight, out _, out var failedRate);
                Assert.AreEqual(0.0, rates[3], 1e-12, $"weight {weight} Lv4");
                Assert.AreEqual(0.0, rates[4], 1e-12, $"weight {weight} Lv5");
                Assert.AreEqual(1.0, rates.Sum() + failedRate, 1e-6, $"weight {weight} sum");
            }
        }

        #region TestHelper

        // 損失0のTier1の表で混ぜた結果を [Lv1..Lv5, 失敗ダイ] に並べる
        // Lay the lossless tier-1 blend out as [Lv1..Lv5, failed die]
        private static double[] Blend(double approachWeight)
        {
            var rates = ChipYieldFormula.DicingRates(NoModuleLevels, NoModuleFailed, Tier1Levels, Tier1Failed, 0.0, 0.0, approachWeight, out _, out var failedRate);
            return rates.Append(failedRate).ToArray();
        }

        private static void AssertRates(double[] actual, params double[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], actual[i], 1e-6, $"index {i}");
        }

        #endregion
    }
}
