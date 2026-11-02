using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class ChipYieldFormulaTest
    {
        // Part B の初期値。行はクラスA→Dの順（品質倍率・層失敗率・ダイ損失率）
        // Initial values from Part B, rows ordered class A to D (quality multiplier, layer fail rate, die loss rate)
        private static readonly (float qualityMultiplier, float layerFailRate, float dieLossRate)[] ClassRows =
        {
            (1.0f, 0.10f, 0.15f),
            (0.75f, 0.15f, 0.22f),
            (0.5f, 0.20f, 0.30f),
            (0.25f, 0.25f, 0.38f),
        };

        // v8 の品質Tier別ダイ表（Lv1..Lv5）と失敗ダイ
        // v8 per-quality-tier die tables (Lv1..Lv5) and the failed die
        private static readonly (float[] levels, float failed)[] TierTables =
        {
            (new[] { 0.2f, 0f, 0f, 0f, 0f }, 0.8f),
            (new[] { 0.7f, 0.1f, 0f, 0f, 0f }, 0.2f),
            (new[] { 0.1f, 0.7f, 0.1f, 0f, 0f }, 0.1f),
            (new[] { 0.04f, 0.08f, 0.7f, 0.1f, 0f }, 0.08f),
            (new[] { 0.02f, 0.04f, 0.06f, 0.7f, 0.1f }, 0.08f),
            (new[] { 0.02f, 0.03f, 0.05f, 0.1f, 0.7f }, 0.1f),
        };

        private static readonly float[] Tier2Levels = { 0.1f, 0.7f, 0.1f, 0f, 0f };

        private const double MaxReduction = 0.8;
        private const double MaxRareShift = 0.2;
        private const int LayerCount = 3;
        private const int DiesPerWafer = 300;

        [Test]
        public void ReducedRateClampsQualityReductionTest()
        {
            Assert.AreEqual(0.07, ChipYieldFormula.ReducedRate(0.10, 0.3, 0.8), 1e-9);
            Assert.AreEqual(0.02, ChipYieldFormula.ReducedRate(0.10, 2.0, 0.8), 1e-9);
            Assert.AreEqual(0.10, ChipYieldFormula.ReducedRate(0.10, 0.0, 0.8), 1e-9);
        }

        [Test]
        public void OneModuleInClassAKeepsTableTest()
        {
            // 1個・クラスAではシフト0で表のまま
            // One module in class A means zero shift, so the table stays
            var rates = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 0.06, 1.0, 0.06, MaxRareShift);
            Assert.AreEqual(new[] { 0.1, 0.7, 0.1, 0.0, 0.0, 0.1 }, rates.Select(r => Math.Round(r, 6)).ToArray());
        }

        [Test]
        public void MoreModulesMoveFailureToRareTest()
        {
            // 3個でシフト0.12。失敗枠0.2が0.08へ比例縮小し、レアが0.22になる
            // Three modules shift 0.12: the 0.2 failure slot shrinks proportionally to 0.08 and the rare becomes 0.22
            var rates = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 0.18, 1.0, 0.06, MaxRareShift);
            Assert.AreEqual(0.04, rates[0], 1e-6);
            Assert.AreEqual(0.7, rates[1], 1e-6);
            Assert.AreEqual(0.22, rates[2], 1e-6);
            Assert.AreEqual(0.04, rates[5], 1e-6);
        }

        [Test]
        public void LowClassMovesRareToFailedDieTest()
        {
            // クラスD(0.25)1個はシフト−0.045。レアが減り失敗ダイが増える
            // One module in class D (0.25) shifts −0.045, lowering the rare and raising the failed die
            var rates = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 0.06, 0.25, 0.06, MaxRareShift);
            Assert.AreEqual(0.055, rates[2], 1e-6);
            Assert.AreEqual(0.145, rates[5], 1e-6);
            Assert.AreEqual(0.1, rates[0], 1e-6);
        }

        [Test]
        public void ShiftIsCappedAndSumStaysOneTest()
        {
            // 上限0.2で頭打ち、どの入力でも合計1
            // The shift saturates at 0.2 and every input keeps the sum at one
            var rates = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 5.0, 1.0, 0.06, MaxRareShift);
            Assert.AreEqual(0.2, rates[2] - 0.1, 1e-6);
            Assert.AreEqual(1.0, rates.Sum(), 1e-6);

            // 失敗枠より大きい上限でも失敗枠を使い切るだけで負にならない
            // A cap above the failure slot only drains it and never goes negative
            var drained = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 5.0, 1.0, 0.06, 1.0);
            Assert.AreEqual(0.3, drained[2], 1e-6);
            Assert.IsTrue(drained.All(r => r >= -1e-12));
            Assert.AreEqual(1.0, drained.Sum(), 1e-6);

            // 負のシフトはレアを0までしか減らさない
            // A negative shift drains the rare to zero at most
            var floored = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 0.0, 1.0, 5.0, MaxRareShift);
            Assert.AreEqual(0.0, floored[2], 1e-6);
            Assert.AreEqual(1.0, floored.Sum(), 1e-6);
        }

        [Test]
        public void TierZeroAndTopTierNeverShiftTest()
        {
            // Tier0（モジュール無し）と最大Tier（レア無し）は表のまま
            // Tier 0 (no module) and the top tier (no rare) keep the table
            var none = ChipYieldFormula.ShiftedDieRates(new[] { 0.2f, 0f, 0f, 0f, 0f }, 0.8f, 0, 0, 1.0, 0, MaxRareShift);
            Assert.AreEqual(0.2, none[0], 1e-6);
            var top = ChipYieldFormula.ShiftedDieRates(new[] { 0.02f, 0.03f, 0.05f, 0.1f, 0.7f }, 0.1f, 5, 0.6, 1.0, 0.12, MaxRareShift);
            Assert.AreEqual(0.7, top[4], 1e-6);
            Assert.AreEqual(0.1, top[5], 1e-6);
        }

        [Test]
        public void QualityGateHoldsForEveryShiftTest()
        {
            // どのシフトでも Tier t で Lv t+2 以上は0のまま
            // At any shift, tier t keeps Lv t+2 and above at zero
            for (var tier = 0; tier < TierTables.Length; tier++)
            {
                foreach (var qualitySum in new[] { 0.0, 0.1, 0.5, 5.0 })
                {
                    var rates = ChipYieldFormula.ShiftedDieRates(TierTables[tier].levels, TierTables[tier].failed, tier, qualitySum, 1.0, 0.1, MaxRareShift);
                    for (var levelIndex = tier + 1; levelIndex < 5; levelIndex++) Assert.AreEqual(0.0, rates[levelIndex], 1e-12, $"tier {tier} Lv{levelIndex + 1}");
                    Assert.AreEqual(1.0, rates.Sum(), 1e-6, $"tier {tier} sum");
                }
            }
        }

        [Test]
        public void WorkedExampleExpectedDiesTest()
        {
            // クラスA・Tier2を3個(合計0.18): シフト0.12、層失敗0.082、ダイ損失0.123、生存=0.918^3×300×0.877=203.5396
            // Class A, three tier-2 modules (sum 0.18): shift 0.12, layer fail 0.082, die loss 0.123, survivors 0.918^3x300x0.877=203.5396
            var expected = ExpectedDies(ClassRows[0], 0.18, 2, 0.06);
            Assert.AreEqual(8.1416, expected[0], 1e-3);
            Assert.AreEqual(142.4777, expected[1], 1e-3);
            Assert.AreEqual(44.7787, expected[2], 1e-3);
            Assert.AreEqual(0.0, expected[3], 1e-9);
            Assert.AreEqual(0.0, expected[4], 1e-9);
        }

        [Test]
        public void ClassImprovementNeverReducesGoodTotalOrUpperLevelsTest()
        {
            // クラスを上げると、良品の合計とレア以上の期待値が減らない（Tier1〜4・1〜5個の総当たり）
            // A better class never lowers the good total or the rare-and-above count (tiers 1-4 x 1-5 modules)
            const double effectPerTier = 0.03;
            for (var tier = 1; tier <= 4; tier++)
            {
                for (var moduleCount = 1; moduleCount <= 5; moduleCount++)
                {
                    var effectValue = effectPerTier * tier;
                    var qualitySum = effectValue * moduleCount;
                    for (var better = 0; better < ClassRows.Length - 1; better++)
                    {
                        var betterDies = ExpectedDies(ClassRows[better], qualitySum, tier, effectValue);
                        var worseDies = ExpectedDies(ClassRows[better + 1], qualitySum, tier, effectValue);
                        var label = $"tier {tier} count {moduleCount} rows {better}/{better + 1}";
                        Assert.GreaterOrEqual(Sum(betterDies, 0) + 1e-9, Sum(worseDies, 0), $"total {label}");
                        Assert.GreaterOrEqual(Sum(betterDies, tier) + 1e-9, Sum(worseDies, tier), $"rare+ {label}");
                    }
                }
            }

            #region Internal
            static double Sum(IEnumerable<double> values, int fromIndex) => values.Skip(fromIndex).Sum();
            #endregion
        }

        #region TestHelper

        // 本番の式（割引率・シフト後の割合）を通し、投入1枚あたりのレベル別良品ダイ期待値を出す
        // Run the production formulas (reduced rates, shifted rates) and get the expected good dies per wafer by level
        private static double[] ExpectedDies((float qualityMultiplier, float layerFailRate, float dieLossRate) classRow, double qualitySum, int tier, double referenceEffectValue)
        {
            var layerFail = ChipYieldFormula.ReducedRate(classRow.layerFailRate, qualitySum, MaxReduction);
            var dieLoss = ChipYieldFormula.ReducedRate(classRow.dieLossRate, qualitySum, MaxReduction);
            var (levels, failed) = TierTables[tier];
            var rates = ChipYieldFormula.ShiftedDieRates(levels, failed, tier, qualitySum, classRow.qualityMultiplier, referenceEffectValue, MaxRareShift);

            // 全層成功×ダイ生存×シフト後の割合
            // All layers pass x die survival x shifted rates
            var survivingDies = Math.Pow(1d - layerFail, LayerCount) * DiesPerWafer * (1d - dieLoss);
            var expected = new double[levels.Length];
            for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++) expected[levelIndex] = survivingDies * rates[levelIndex];
            return expected;
        }

        #endregion
    }
}
