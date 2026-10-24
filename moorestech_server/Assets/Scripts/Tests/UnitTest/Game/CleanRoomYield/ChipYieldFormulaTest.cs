using System.Collections.Generic;
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

        private static readonly float[][] GradeDieTables =
        {
            new[] { 1f },
            new[] { 0.7f, 0.3f },
            new[] { 0.55f, 0.3f, 0.15f },
            new[] { 0.45f, 0.3f, 0.17f, 0.08f },
            new[] { 0.4f, 0.28f, 0.18f, 0.09f, 0.05f },
        };

        private const double MaxPromotionChance = 0.9;
        private const double MaxReduction = 0.8;
        private const int LayerCount = 3;
        private const int DiesPerWafer = 300;
        private const int GradeCount = 5;
        private const int DieLevelCount = 5;

        [TestCase(0, 5, 1)]
        [TestCase(1, 5, 2)]
        [TestCase(4, 5, 5)]
        [TestCase(7, 5, 5)]
        [TestCase(2, 3, 3)]
        public void MaxWaferGradeIsHighestTierPlusOneTest(int highestTier, int gradeCount, int expected)
        {
            Assert.AreEqual(expected, ChipYieldFormula.MaxWaferGrade(highestTier, gradeCount));
        }

        [Test]
        public void PromotionChanceIsClampedProductTest()
        {
            Assert.AreEqual(0.9, ChipYieldFormula.PromotionChance(1.0, 1.0, 0.9), 1e-9);
            Assert.AreEqual(0.2, ChipYieldFormula.PromotionChance(0.4, 0.5, 0.9), 1e-9);
            Assert.AreEqual(0.0, ChipYieldFormula.PromotionChance(0.0, 1.0, 0.9), 1e-9);
        }

        [Test]
        public void ReducedRateClampsQualityReductionTest()
        {
            Assert.AreEqual(0.07, ChipYieldFormula.ReducedRate(0.10, 0.3, 0.8), 1e-9);
            Assert.AreEqual(0.02, ChipYieldFormula.ReducedRate(0.10, 2.0, 0.8), 1e-9);
            Assert.AreEqual(0.10, ChipYieldFormula.ReducedRate(0.10, 0.0, 0.8), 1e-9);
        }

        [Test]
        public void WorkedExampleExpectedDiesTest()
        {
            // クラスA・品質合計0.6・最高tier2: u=0.6、層失敗0.04、ダイ損失0.06、生存=0.96^3×300×0.94=249.4956
            // Class A, quality sum 0.6, top tier 2: u=0.6, layer fail 0.04, die loss 0.06, survivors 0.96^3x300x0.94=249.4956
            var expected = ExpectedDies(ClassRows[0], 0.6, 2);
            Assert.AreEqual(191.1136, expected[0], 1e-3);
            Assert.AreEqual(44.9092, expected[1], 1e-3);
            Assert.AreEqual(13.4728, expected[2], 1e-3);
            Assert.AreEqual(0.0, expected[3], 1e-9);
            Assert.AreEqual(0.0, expected[4], 1e-9);
        }

        [Test]
        public void QualityGateNeverYieldsAboveTierPlusOneTest()
        {
            // 最高tier t の構成では Lv t+2 以上の期待値が0、Lv t+1 は正
            // With top tier t, every level from t+2 upward has zero expectation and level t+1 is positive
            for (var highestTier = 0; highestTier <= 3; highestTier++)
            {
                foreach (var classRow in ClassRows)
                {
                    var expected = ExpectedDies(classRow, 1.5, highestTier);
                    Assert.Greater(expected[highestTier], 0.0, $"tier {highestTier}");
                    for (var levelIndex = highestTier + 1; levelIndex < DieLevelCount; levelIndex++)
                    {
                        Assert.AreEqual(0.0, expected[levelIndex], 1e-12, $"tier {highestTier} level {levelIndex + 1}");
                    }
                }
            }
        }

        [Test]
        public void ClassImprovementNeverReducesGoodTotalOrUpperLevelsTest()
        {
            // クラスを上げても良品合計とLv2以上は減らない。Lv1は減ってよい
            // A better class never lowers the good total or the Lv2+ count; Lv1 may drop
            var moduleConfigs = new (double qualitySum, int highestTier)[] { (0.0, 0), (0.3, 1), (0.6, 2), (0.9, 4), (1.5, 4) };
            foreach (var (qualitySum, highestTier) in moduleConfigs)
            {
                for (var better = 0; better < ClassRows.Length - 1; better++)
                {
                    var betterDies = ExpectedDies(ClassRows[better], qualitySum, highestTier);
                    var worseDies = ExpectedDies(ClassRows[better + 1], qualitySum, highestTier);
                    Assert.GreaterOrEqual(Sum(betterDies, 0) + 1e-9, Sum(worseDies, 0), $"total sum {qualitySum} rows {better}/{better + 1}");
                    Assert.GreaterOrEqual(Sum(betterDies, 1) + 1e-9, Sum(worseDies, 1), $"Lv2+ sum {qualitySum} rows {better}/{better + 1}");
                }
            }

            #region Internal

            static double Sum(IReadOnlyList<double> values, int fromIndex)
            {
                var total = 0.0;
                for (var i = fromIndex; i < values.Count; i++) total += values[i];
                return total;
            }

            #endregion
        }

        #region TestHelper

        // 本番の式（上限・昇格確率・割引率）を通し、閉形式で投入1枚あたりのレベル別良品ダイ期待値を出す
        // Run the production formulas (cap, promotion chance, reduced rates) and get the closed-form expected good dies per wafer by level
        private static double[] ExpectedDies((float qualityMultiplier, float layerFailRate, float dieLossRate) classRow, double qualitySum, int highestTier)
        {
            var maxGrade = ChipYieldFormula.MaxWaferGrade(highestTier, GradeCount);
            var chance = ChipYieldFormula.PromotionChance(qualitySum, classRow.qualityMultiplier, MaxPromotionChance);
            var layerFail = ChipYieldFormula.ReducedRate(classRow.layerFailRate, qualitySum, MaxReduction);
            var dieLoss = ChipYieldFormula.ReducedRate(classRow.dieLossRate, qualitySum, MaxReduction);

            // 全層成功×ダイ生存×グレード確率×グレード別ダイ表
            // All layers pass x die survival x grade probability x per-grade die table
            var expected = new double[DieLevelCount];
            var survivingDies = System.Math.Pow(1d - layerFail, LayerCount) * DiesPerWafer * (1d - dieLoss);
            var gradeProbabilities = WaferGradeProbabilities(chance, maxGrade);
            for (var gradeIndex = 0; gradeIndex < gradeProbabilities.Length; gradeIndex++)
            {
                var table = GradeDieTables[gradeIndex];
                for (var levelIndex = 0; levelIndex < table.Length; levelIndex++) expected[levelIndex] += gradeProbabilities[gradeIndex] * survivingDies * table[levelIndex];
            }
            return expected;

            #region Internal

            // グレードgの確率。上限未満は u^(g-1)(1-u)、上限は u^(max-1)（ChipYieldDraw.DrawWaferGrade の閉形式）
            // Probability of grade g: u^(g-1)(1-u) below the cap, u^(max-1) at the cap (the closed form of ChipYieldDraw.DrawWaferGrade)
            static double[] WaferGradeProbabilities(double promotionChance, int maxGrade)
            {
                var probabilities = new double[maxGrade];
                var reach = 1d;
                for (var grade = 1; grade <= maxGrade; grade++)
                {
                    probabilities[grade - 1] = grade == maxGrade ? reach : reach * (1d - promotionChance);
                    reach *= promotionChance;
                }
                return probabilities;
            }

            #endregion
        }

        #endregion
    }
}
