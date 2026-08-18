using Client.Game.InGame.Environment.Terrain.Visual.Detail;
using Client.Game.InGame.Environment.Terrain.Visual.Detail.Distance;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.DistanceField
{
    /// <summary>
    ///     距離フィルタの最大探索半径の算出を固定する。この値はSDFの打ち切り半径とタイル切り出しのhalo幅を兼ねるため、
    ///     小さく出るとタイル境界の外の木が距離場から抜け、境界に沿った帯として静かに現れる
    ///     Pins the distance filters' max search radius. It doubles as the SDF cutoff and the tile slice halo, so
    ///     under-reporting drops trees just outside the tile and surfaces only as a band along the boundary
    /// </summary>
    public class DetailDistanceRadiusTest
    {
        [Test]
        public void AddsTheUpperSmoothnessToTheUpperRangeSoTheFalloffTailStaysInside()
        {
            // range.yだけでは減衰の裾が切れる。裾の内側にある木を見落とすと境界画素だけ密度が跳ねる
            // Taking range.y alone would clip the falloff tail; missing a tree inside it makes only the edge pixels jump
            var entries = new[] { CreateEntryWithTreeFilter(range: new Vector2(5f, 30f), smoothness: new Vector2(3f, 7f)) };

            Assert.That(DetailDistanceRadius.ForTrees(entries), Is.EqualTo(37f));
        }

        [Test]
        public void TakesTheLargestRadiusAcrossEntries()
        {
            var entries = new[]
            {
                CreateEntryWithTreeFilter(range: new Vector2(0f, 12f), smoothness: new Vector2(0f, 1f)),
                CreateEntryWithTreeFilter(range: new Vector2(0f, 40f), smoothness: new Vector2(0f, 2f)),
                CreateEntryWithTreeFilter(range: new Vector2(0f, 8f), smoothness: new Vector2(0f, 0f)),
            };

            Assert.That(DetailDistanceRadius.ForTrees(entries), Is.EqualTo(42f));
        }

        [Test]
        public void IgnoresDisabledFilters()
        {
            var entries = new[] { CreateEntryWithTreeFilter(range: new Vector2(0f, 40f), smoothness: new Vector2(0f, 2f)) };
            entries[0].treeDistanceFilter.enabled = false;

            Assert.That(DetailDistanceRadius.ForTrees(entries), Is.EqualTo(0f));
        }

        [Test]
        public void ReadsTheTreeAndObjectFiltersSeparately()
        {
            // 2本を混ぜると片方の半径でもう片方の距離場を打ち切る。木と岩でフィルタ設定が違うのが普通なので必ず割れる
            // Mixing the two would cut one distance field at the other's radius; tree and rock filters normally differ, so it always breaks
            var entry = CreateEntryWithTreeFilter(range: new Vector2(0f, 40f), smoothness: new Vector2(0f, 2f));
            entry.objectDistanceFilter.enabled = true;
            entry.objectDistanceFilter.range = new Vector2(0f, 6f);
            entry.objectDistanceFilter.smoothness = new Vector2(0f, 1f);

            var entries = new[] { entry };

            Assert.That(DetailDistanceRadius.ForTrees(entries), Is.EqualTo(42f));
            Assert.That(DetailDistanceRadius.ForObjects(entries), Is.EqualTo(7f));
        }

        private static DetailEntry CreateEntryWithTreeFilter(Vector2 range, Vector2 smoothness)
        {
            var entry = DetailTestConfigBuilder.CreateEntry(1f, 8);
            entry.treeDistanceFilter.enabled = true;
            entry.treeDistanceFilter.range = range;
            entry.treeDistanceFilter.smoothness = smoothness;
            return entry;
        }
    }
}
