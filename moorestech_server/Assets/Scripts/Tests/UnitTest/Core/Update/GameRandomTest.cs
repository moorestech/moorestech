using System;
using Core.Update;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Update
{
    public class GameRandomTest
    {
        [Test]
        public void 同じシードからは同じ列が出る()
        {
            GameRandom.Reseed(12345UL);
            var first = new[] { GameRandom.NextUlong(), GameRandom.NextUlong(), GameRandom.NextUlong() };
            GameRandom.Reseed(12345UL);
            var second = new[] { GameRandom.NextUlong(), GameRandom.NextUlong(), GameRandom.NextUlong() };
            CollectionAssert.AreEqual(first, second, "同じシードで列が一致しない");
        }

        [Test]
        public void 状態を書き出して戻すと続きの列が一致する()
        {
            GameRandom.Reseed(777UL);
            GameRandom.NextUlong();
            var state = GameRandom.ExportState();
            var expected = new[] { GameRandom.NextInt(), GameRandom.Next(0, 10), GameRandom.NextInt() };
            var expectedGuid = GameRandom.NextGuid();
            var expectedDouble = GameRandom.NextDouble();

            GameRandom.RestoreState(state);
            var actual = new[] { GameRandom.NextInt(), GameRandom.Next(0, 10), GameRandom.NextInt() };
            CollectionAssert.AreEqual(expected, actual, "復元後の列が一致しない");
            Assert.AreEqual(expectedGuid, GameRandom.NextGuid(), "復元後のGuidが一致しない");
            Assert.AreEqual(expectedDouble, GameRandom.NextDouble(), "復元後のdoubleが一致しない");
        }

        [Test]
        public void 範囲付き乱数は範囲内に収まりdoubleは0以上1未満()
        {
            GameRandom.Reseed(1UL);
            for (var i = 0; i < 10000; i++)
            {
                var value = GameRandom.Next(-3, 5);
                Assert.IsTrue(-3 <= value && value < 5, $"範囲外: {value}");
                var d = GameRandom.NextDouble();
                Assert.IsTrue(0d <= d && d < 1d, $"範囲外: {d}");
            }
        }

        [Test]
        public void 状態の長さが不正なら例外()
        {
            Assert.Throws<ArgumentException>(() => GameRandom.RestoreState(new ulong[3]));
        }

        [Test]
        public void tickを復元できる()
        {
            GameUpdater.RestoreCurrentTick(4242UL);
            Assert.AreEqual(4242UL, GameUpdater.CurrentTick);
        }
    }
}
