using Game.Block.Blocks.CleanRoom.Machine.Yield;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class ChipYieldDrawTest
    {
        private static readonly float[] GradeThreeTable = { 0.55f, 0.3f, 0.15f };

        [Test]
        public void SameSeedReproducesEveryDrawTest()
        {
            // 同じシードからはグレード・層判定・ダイ振り分けまで同じ結果になる
            // The same seed reproduces the grade, the layer roll and the die split
            var first = new CleanRoomDrawRandom(123456789L);
            var second = new CleanRoomDrawRandom(123456789L);
            Assert.AreEqual(ChipYieldDraw.DrawWaferGrade(0.6, 5, first), ChipYieldDraw.DrawWaferGrade(0.6, 5, second));
            Assert.AreEqual(ChipYieldDraw.DrawLayerFailure(0.3, first), ChipYieldDraw.DrawLayerFailure(0.3, second));
            var firstDicing = ChipYieldDraw.DrawDicing(300, 0.2, GradeThreeTable, first);
            var secondDicing = ChipYieldDraw.DrawDicing(300, 0.2, GradeThreeTable, second);
            CollectionAssert.AreEqual(firstDicing.GoodDieCountsByLevel, secondDicing.GoodDieCountsByLevel);
            Assert.AreEqual(firstDicing.FailedDieCount, secondDicing.FailedDieCount);
        }

        [Test]
        public void WaferGradeStaysWithinCapTest()
        {
            // 昇格確率1でも上限で止まり、0なら常にG1
            // Even a certain promotion stops at the cap, and zero chance always yields G1
            for (var seed = 0L; seed < 200; seed++)
            {
                Assert.AreEqual(3, ChipYieldDraw.DrawWaferGrade(1.0, 3, new CleanRoomDrawRandom(seed)));
                Assert.AreEqual(1, ChipYieldDraw.DrawWaferGrade(0.0, 5, new CleanRoomDrawRandom(seed)));
                Assert.AreEqual(1, ChipYieldDraw.DrawWaferGrade(0.9, 1, new CleanRoomDrawRandom(seed)));
            }
        }

        [Test]
        public void WaferGradeFrequencyMatchesClosedFormTest()
        {
            // 20000回の頻度が閉形式 [0.4, 0.24, 0.36] に収まる（標準誤差は約0.0035）
            // 20000 draws land on the closed form [0.4, 0.24, 0.36] (standard error ~0.0035)
            const int drawCount = 20000;
            var counts = new int[3];
            var random = new CleanRoomDrawRandom(42L);
            for (var i = 0; i < drawCount; i++) counts[ChipYieldDraw.DrawWaferGrade(0.6, 3, random) - 1]++;
            Assert.AreEqual(0.4, counts[0] / (double)drawCount, 0.02);
            Assert.AreEqual(0.24, counts[1] / (double)drawCount, 0.02);
            Assert.AreEqual(0.36, counts[2] / (double)drawCount, 0.02);
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
            // 良品の各レベルと失敗の合計は常に1枚あたりのダイ数に一致する
            // Good dies across levels plus failed dies always equal the dies per wafer
            for (var seed = 0L; seed < 100; seed++)
            {
                var result = ChipYieldDraw.DrawDicing(300, 0.3, GradeThreeTable, new CleanRoomDrawRandom(seed));
                Assert.AreEqual(3, result.GoodDieCountsByLevel.Count);
                var total = result.FailedDieCount;
                foreach (var count in result.GoodDieCountsByLevel) total += count;
                Assert.AreEqual(300, total);
            }
        }

        [Test]
        public void DicingMeanMatchesExpectationTest()
        {
            // 損失0.1・表[.55,.3,.15]: 良品の平均270、Lv3の平均40.5（2000枚の標準誤差は約0.12と0.13）
            // Loss 0.1 with table [.55,.3,.15]: good mean 270, Lv3 mean 40.5 (standard errors ~0.12 and ~0.13 over 2000 wafers)
            const int waferCount = 2000;
            var goodTotal = 0L;
            var levelThreeTotal = 0L;
            var random = new CleanRoomDrawRandom(2026L);
            for (var i = 0; i < waferCount; i++)
            {
                var result = ChipYieldDraw.DrawDicing(300, 0.1, GradeThreeTable, random);
                foreach (var count in result.GoodDieCountsByLevel) goodTotal += count;
                levelThreeTotal += result.GoodDieCountsByLevel[2];
            }
            Assert.AreEqual(270.0, goodTotal / (double)waferCount, 1.0);
            Assert.AreEqual(40.5, levelThreeTotal / (double)waferCount, 1.0);
        }

        [Test]
        public void GradeOneTableYieldsOnlyLevelOneTest()
        {
            var result = ChipYieldDraw.DrawDicing(300, 0.15, new[] { 1f }, new CleanRoomDrawRandom(9L));
            Assert.AreEqual(1, result.GoodDieCountsByLevel.Count);
            Assert.AreEqual(300, result.GoodDieCountsByLevel[0] + result.FailedDieCount);
        }
    }
}
