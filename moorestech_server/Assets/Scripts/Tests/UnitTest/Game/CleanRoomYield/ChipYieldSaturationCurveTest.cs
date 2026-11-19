using System.Linq;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class ChipYieldSaturationCurveTest
    {
        // v8 のクラスA→D（品質倍率・ダイ損失率）
        // v8 classes A to D (quality multiplier, die loss rate)
        private static readonly (double qualityMultiplier, double dieLossRate)[] ClassRows =
        {
            (1.0, 0.15),
            (0.75, 0.3),
            (0.5, 0.45),
            (0.25, 0.6),
        };

        // v8 の品質Tier別ダイ表（Lv1..Lv5）と失敗ダイ。Tier0はモジュール無し、Tier1以上は十分入れたときの表
        // v8 per-quality-tier die tables (Lv1..Lv5) and the failed die; tier 0 is without modules, tier 1+ is the full-module table
        private static readonly (float[] levels, float failed)[] TierTables =
        {
            (new[] { 0.2f, 0f, 0f, 0f, 0f }, 0.8f),
            (new[] { 0.83125f, 0.11875f, 0f, 0f, 0f }, 0.05f),
            (new[] { 0.025f, 0.83125f, 0.11875f, 0f, 0f }, 0.025f),
            (new[] { 0.01f, 0.02f, 0.83125f, 0.11875f, 0f }, 0.02f),
            (new[] { 0.005f, 0.01f, 0.015f, 0.83125f, 0.11875f }, 0.02f),
            (new[] { 0.005f, 0.0075f, 0.0125f, 0.025f, 0.925f }, 0.025f),
        };

        // v8 の品質モジュールLv1〜5の効果値（添字=Tier）
        // v8 quality module effect values for Lv1-5 (index = tier)
        private static readonly double[] EffectValueByTier = { 0, 0.04, 0.06, 0.08, 0.10, 0.12 };

        private const double QualityApproachScale = 0.1335;
        private const double MaxLossReduction = 0.8;
        private const int MaxModuleCount = 24;

        [Test]
        public void V8ClassCNineLv1ModulesMatchesTodayTest()
        {
            // クラスC・Lv1を9個で、ウェハー1枚あたりの良品が今と同じ約66.86%
            // Class C with nine Lv1 keeps good dies per wafer at about 66.86%, the same as today
            var dies = GoodDieShares(ClassRows[2], 1, 9);
            Assert.AreEqual(0.6686, dies.Sum(), 5e-4);
            Assert.AreEqual(0.5886, dies[0], 5e-4);
            Assert.AreEqual(0.0800, dies[1], 5e-4);
        }

        [Test]
        public void EveryAddedModuleRaisesGoodDiesTest()
        {
            // どのクラス・Tierでも、0→24個の1個ごとに良品の合計が厳密に増える
            // For every class and tier, each added module from 0 to 24 strictly raises the good total
            for (var tier = 1; tier < TierTables.Length; tier++)
            {
                foreach (var classRow in ClassRows)
                {
                    for (var count = 1; count <= MaxModuleCount; count++)
                    {
                        var before = count == 1 ? GoodDieShares(classRow, 0, 0).Sum() : GoodDieShares(classRow, tier, count - 1).Sum();
                        Assert.Greater(GoodDieShares(classRow, tier, count).Sum(), before, $"tier {tier} class {classRow} count {count}");
                    }
                }
            }
        }

        [Test]
        public void GainShrinksWithEveryAddedModuleTest()
        {
            // 1個増やしたときの増分は、その前の増分以下（2個目以降）
            // The gain from one more module never exceeds the previous gain (from the second module on)
            for (var tier = 1; tier < TierTables.Length; tier++)
            {
                foreach (var classRow in ClassRows)
                {
                    for (var count = 2; count < MaxModuleCount; count++)
                    {
                        var previousGain = GoodDieShares(classRow, tier, count).Sum() - GoodDieShares(classRow, tier, count - 1).Sum();
                        var nextGain = GoodDieShares(classRow, tier, count + 1).Sum() - GoodDieShares(classRow, tier, count).Sum();
                        Assert.LessOrEqual(nextGain, previousGain + 1e-12, $"tier {tier} class {classRow} count {count}");
                    }
                }
            }
        }

        [Test]
        public void ClassImprovementNeverReducesGoodTotalOrUpperLevelsTest()
        {
            // クラスを上げると、良品の合計とレア以上の期待値が減らない（Tier1〜4・1〜24個の総当たり）
            // A better class never lowers the good total or the rare-and-above share (tiers 1-4 x 1-24 modules)
            for (var tier = 1; tier <= 4; tier++)
            {
                for (var count = 1; count <= MaxModuleCount; count++)
                {
                    for (var better = 0; better < ClassRows.Length - 1; better++)
                    {
                        var betterDies = GoodDieShares(ClassRows[better], tier, count);
                        var worseDies = GoodDieShares(ClassRows[better + 1], tier, count);
                        var label = $"tier {tier} count {count} rows {better}/{better + 1}";
                        Assert.GreaterOrEqual(betterDies.Sum() + 1e-9, worseDies.Sum(), $"total {label}");
                        Assert.GreaterOrEqual(betterDies.Skip(tier).Sum() + 1e-9, worseDies.Skip(tier).Sum(), $"rare+ {label}");
                    }
                }
            }
        }

        // 本番の式（近づき度→損失と表の混合）を通し、ダイシング投入ウェハー1枚あたりのレベル別良品ダイの割合を出す
        // Run the production formulas (approach weight, then loss and table blend) for the good-die share per diced wafer by level
        private static double[] GoodDieShares((double qualityMultiplier, double dieLossRate) classRow, int tier, int moduleCount)
        {
            var weight = ChipYieldFormula.ApproachWeight(EffectValueByTier[tier] * moduleCount, classRow.qualityMultiplier, QualityApproachScale);
            var rates = ChipYieldFormula.DicingRates(TierTables[0].levels, TierTables[0].failed, TierTables[tier].levels, TierTables[tier].failed, classRow.dieLossRate, MaxLossReduction, weight, out var dieLossRate, out _);
            return rates.Select(rate => rate * (1d - dieLossRate)).ToArray();
        }
    }
}
