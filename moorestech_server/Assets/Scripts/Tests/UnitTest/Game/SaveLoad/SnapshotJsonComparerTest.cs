using Game.SaveLoad.Snapshot;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SnapshotJsonComparerTest
    {
        [Test]
        public void settingの差は無視しそれ以外の差はパスで報告する()
        {
            var a = "{\"world\":[{\"X\":1,\"state\":{\"k\":{\"v\":1}}}],\"setting\":{\"t\":\"2026-01-01\"},\"currentTick\":5}";
            var b = "{\"world\":[{\"X\":1,\"state\":{\"k\":{\"v\":2}}}],\"setting\":{\"t\":\"2026-09-11\"},\"currentTick\":5}";
            var same = "{\"world\":[{\"X\":1,\"state\":{\"k\":{\"v\":1}}}],\"setting\":{\"t\":\"2099-01-01\"},\"currentTick\":5}";

            Assert.IsTrue(SnapshotJsonComparer.Compare(a, same).Equal);
            var diff = SnapshotJsonComparer.Compare(a, b);
            Assert.IsFalse(diff.Equal);
            Assert.AreEqual(1, diff.Differences.Count);
            StringAssert.Contains("world[0].state.k.v", diff.Differences[0]);
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
