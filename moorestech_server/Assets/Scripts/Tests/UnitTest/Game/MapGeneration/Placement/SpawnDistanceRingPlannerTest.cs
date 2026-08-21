using Game.MapGeneration.Pipeline.Generators.Util;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    // 鉱脈とmapObject散布が共有するスポーン距離リング化の規則（昇順・負値は無限・重複縮退）を固定する。
    // Pins the spawn-distance ring rules shared by veins and object scatter (ascending, negative = infinite, duplicates degenerate).
    public class SpawnDistanceRingPlannerTest
    {
        [Test]
        public void 外半径昇順に並び負値は無限の最外周になる()
        {
            var rings = SpawnDistanceRingPlanner.BuildRings(new[] { 350f, 250f, -1f });

            Assert.AreEqual(3, rings.Count);
            Assert.AreEqual(1, rings[0].BandIndex);
            Assert.AreEqual(0f, rings[0].Inner);
            Assert.AreEqual(250f, rings[0].Outer);
            Assert.AreEqual(0, rings[1].BandIndex);
            Assert.AreEqual(250f, rings[1].Inner);
            Assert.AreEqual(350f, rings[1].Outer);
            Assert.AreEqual(2, rings[2].BandIndex);
            Assert.AreEqual(350f, rings[2].Inner);
            Assert.AreEqual(float.PositiveInfinity, rings[2].Outer);
        }

        [Test]
        public void 重複した外半径の後者は縮退してリングにならない()
        {
            var rings = SpawnDistanceRingPlanner.BuildRings(new[] { 250f, 250f });

            Assert.AreEqual(1, rings.Count);
            Assert.AreEqual(0, rings[0].BandIndex);
        }

        [Test]
        public void 空配列はリングを作らない()
        {
            Assert.AreEqual(0, SpawnDistanceRingPlanner.BuildRings(new float[0]).Count);
        }

        [Test]
        public void リング判定は内側を含み外側を含まない()
        {
            var ring = new SpawnDistanceRing(0, 250f, 350f);

            Assert.IsTrue(ring.Contains(250f));
            Assert.IsTrue(ring.Contains(349.9f));
            Assert.IsFalse(ring.Contains(350f));
            Assert.IsFalse(ring.Contains(249.9f));
        }
    }
}
