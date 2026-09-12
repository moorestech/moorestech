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

        // 既知ベクトルで定数を固定する。これが無いと SplitMix64 / xoshiro の定数を書き換えても全テストが緑のまま通る
        // Pin the constants with a known vector; without it, altering a SplitMix64 / xoshiro constant leaves every test green
        // 同じベクトルを scripts/save_migration/migrate_block_state_objects.py の自己テストも検査する（C#とPythonの二重実装を突き合わせるため）
        // The migration script self-tests the same vector, which is how the C# and Python implementations stay pinned to each other
        [Test]
        public void シード12345の状態と先頭3語が既知ベクトルと一致する()
        {
            GameRandom.Reseed(12345UL);
            CollectionAssert.AreEqual(
                new ulong[] { 2454886589211414944, 3778200017661327597, 2205171434679333405, 3248800117070709450 },
                GameRandom.ExportState(),
                "Reseed の SplitMix64 が既知ベクトルと違う");

            CollectionAssert.AreEqual(
                new ulong[] { 13720838825685603483, 2398916695208396998, 17770384849984869256 },
                new[] { GameRandom.NextUlong(), GameRandom.NextUlong(), GameRandom.NextUlong() },
                "xoshiro256** の出力が既知ベクトルと違う");
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
