using System.Linq;
using Game.SaveLoad.Snapshot;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SnapshotJsonComparerTest
    {
        // 除外は setting 配下の実時刻2フィールドだけ。それ以外の差はパスで報告される
        // Only the two wall-clock fields under "setting" are excluded; every other difference is reported by path
        private const string SettingWallClock = "\"TotalPlayTimeSeconds\":12.5,\"LastSessionStartDateTime\":\"2026-01-01T00:00:00\"";
        private const string SettingWallClockOther = "\"TotalPlayTimeSeconds\":99.5,\"LastSessionStartDateTime\":\"2026-09-11T00:00:00\"";

        [Test]
        public void settingの実時刻フィールドの差は無視しそれ以外の差はパスで報告する()
        {
            var a = "{\"world\":[{\"X\":1,\"state\":{\"k\":{\"v\":1}}}],\"setting\":{\"SpawnX\":3.0," + SettingWallClock + "},\"currentTick\":5}";
            var b = "{\"world\":[{\"X\":1,\"state\":{\"k\":{\"v\":2}}}],\"setting\":{\"SpawnX\":3.0," + SettingWallClockOther + "},\"currentTick\":5}";
            var same = "{\"world\":[{\"X\":1,\"state\":{\"k\":{\"v\":1}}}],\"setting\":{\"SpawnX\":3.0," + SettingWallClockOther + "},\"currentTick\":5}";

            Assert.IsTrue(SnapshotJsonComparer.Compare(a, same).Equal);
            var diff = SnapshotJsonComparer.Compare(a, b);
            Assert.IsFalse(diff.Equal);
            Assert.AreEqual(1, diff.Differences.Count);
            StringAssert.Contains("world[0].state.k.v", diff.Differences[0]);
        }

        [Test]
        public void スポーン地点と世界作成日時の差は検出する()
        {
            var expected = "{\"setting\":{\"SpawnX\":3.0,\"SpawnY\":0.0,\"WorldCreationDateTime\":\"2026-01-01T00:00:00\"," + SettingWallClock + "}}";
            var actual = "{\"setting\":{\"SpawnX\":0.0,\"SpawnY\":0.0,\"WorldCreationDateTime\":\"2026-02-02T00:00:00\"," + SettingWallClockOther + "}}";

            var diff = SnapshotJsonComparer.Compare(expected, actual);
            Assert.IsFalse(diff.Equal);
            Assert.AreEqual(2, diff.Differences.Count);
            Assert.IsTrue(diff.Differences.Any(difference => difference.StartsWith("setting.SpawnX")), string.Join("\n", diff.Differences));
            Assert.IsTrue(diff.Differences.Any(difference => difference.StartsWith("setting.WorldCreationDateTime")), string.Join("\n", diff.Differences));
        }

        [Test]
        public void 欠落と余剰と要素数違いをパスで報告する()
        {
            var expected = "{\"world\":[{\"X\":1}],\"currentTick\":5}";
            var actual = "{\"world\":[{\"X\":1},{\"X\":2}],\"randomState\":[1]}";

            var diff = SnapshotJsonComparer.Compare(expected, actual);
            Assert.IsFalse(diff.Equal);
            CollectionAssert.Contains(diff.Differences, "world: 要素数が違う expected=1 actual=2");
            CollectionAssert.Contains(diff.Differences, "currentTick: actual に無い");
            CollectionAssert.Contains(diff.Differences, "randomState: expected に無い");
        }
    }
}
