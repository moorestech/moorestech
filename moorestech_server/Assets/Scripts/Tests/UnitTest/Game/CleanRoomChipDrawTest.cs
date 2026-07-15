using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.CleanRoom.Machine;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class CleanRoomChipDrawTest
    {
        // 同一入力は結果と出力IDまで完全に再現されることを固定する
        // Lock in that identical inputs reproduce both the result and output item id
        [Test]
        public void SameArgumentsReturnSameResultAndItemTest()
        {
            var dist = CreateUniformDistribution();
            var seeds = new[] { -12345L, 0L, 1L, 999999L, long.MaxValue / 2 };
            var outputIndexes = new[] { 0, 1, 3 };

            foreach (var seed in seeds)
            foreach (var outputIndex in outputIndexes)
            {
                var firstResult = CleanRoomChipDraw.TryDraw(dist, 4, 0.3, 0.8, seed, outputIndex, out var firstItemId);
                var secondResult = CleanRoomChipDraw.TryDraw(dist, 4, 0.3, 0.8, seed, outputIndex, out var secondItemId);

                Assert.AreEqual(firstResult, secondResult);
                Assert.AreEqual(firstItemId, secondItemId);
            }
        }

        // EUV成功率0は抽選前に常に出力なしへ落ちることを固定する
        // Lock in that zero EUV success always short-circuits to no output
        [Test]
        public void ZeroEuvSuccessRateAlwaysNoOutputTest()
        {
            var dist = CreateUniformDistribution();

            for (var seed = 0L; seed < 1000L; seed++)
            {
                var result = CleanRoomChipDraw.TryDraw(dist, 4, 0, 0, seed, 0, out _);

                Assert.AreEqual(CleanRoomChipDraw.Result.NoOutput, result);
            }
        }

        // EUV成功率1は有効な分布なら必ずチップを出すことを固定する
        // Lock in that full EUV success always draws from a valid distribution
        [Test]
        public void FullEuvSuccessRateAlwaysDrawnTest()
        {
            var dist = CreateUniformDistribution();

            for (var seed = 0L; seed < 1000L; seed++)
            {
                var result = CleanRoomChipDraw.TryDraw(dist, 4, 0, 1, seed, 0, out _);

                Assert.AreEqual(CleanRoomChipDraw.Result.Drawn, result);
            }
        }

        // 最大レベル上限は高レベル候補を切り捨てて再正規化することを固定する
        // Lock in that the max-level ceiling truncates higher candidates and renormalizes
        [Test]
        public void MaxLevelCeilingExcludesHigherLevelsTest()
        {
            var dist = CreateUniformDistribution();

            for (var seed = 0L; seed < 10000L; seed++)
            {
                var result = CleanRoomChipDraw.TryDraw(dist, 2, 0, 1, seed, 0, out var itemId);

                Assert.AreEqual(CleanRoomChipDraw.Result.Drawn, result);
                Assert.IsTrue(itemId.Equals(new ItemId(1)) || itemId.Equals(new ItemId(2)));
            }
        }

        // ダウンビン率1はLv1を除き基礎抽選結果を必ず1段だけ下げることを固定する
        // Lock in that full down-binning lowers only one level while Lv1 remains stable
        [Test]
        public void FullDownBinRateDowngradesOneStepTest()
        {
            var dist = CreateUniformDistribution();

            for (var seed = 0L; seed < 1000L; seed++)
            {
                CleanRoomChipDraw.TryDraw(dist, 4, 0, 1, seed, 0, out var baseItemId);
                var baseLevel = ToLevel(baseItemId);

                var result = CleanRoomChipDraw.TryDraw(dist, 4, 1, 1, seed, 0, out var downBinnedItemId);
                var expectedLevel = baseLevel == 1 ? 1 : baseLevel - 1;

                Assert.AreEqual(CleanRoomChipDraw.Result.Drawn, result);
                Assert.AreEqual(new ItemId(expectedLevel), downBinnedItemId);
            }
        }

        // 等重み分布は大標本で各レベルがおおむね25%へ収束することを固定する
        // Lock in that equal weights converge near 25 percent per level over a large sample
        [Test]
        public void UniformDistributionStaysNearEqualWeightsTest()
        {
            const int trialCount = 40000;
            var dist = CreateUniformDistribution();
            var counts = new int[4];

            for (var seed = 0L; seed < trialCount; seed++)
            {
                var result = CleanRoomChipDraw.TryDraw(dist, 4, 0, 1, seed, 0, out var itemId);

                Assert.AreEqual(CleanRoomChipDraw.Result.Drawn, result);
                counts[ToLevel(itemId) - 1]++;
            }

            for (var level = 0; level < counts.Length; level++)
            {
                Assert.GreaterOrEqual(counts[level], 9200);
                Assert.LessOrEqual(counts[level], 10800);
            }
        }

        private static IReadOnlyList<(int level, double weight, ItemId chipItemId)> CreateUniformDistribution()
        {
            return new (int level, double weight, ItemId chipItemId)[]
            {
                (1, 0.25, new ItemId(1)),
                (2, 0.25, new ItemId(2)),
                (3, 0.25, new ItemId(3)),
                (4, 0.25, new ItemId(4)),
            };
        }

        private static int ToLevel(ItemId itemId)
        {
            for (var level = 1; level <= 4; level++)
            {
                if (itemId.Equals(new ItemId(level))) return level;
            }

            Assert.Fail($"Unexpected chip item id: {itemId}");
            return -1;
        }
    }
}
