using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using static Client.Tests.UnitTest.Terrain.Surround.SurroundTestFixtures;

namespace Client.Tests.UnitTest.Terrain.Surround
{
    /// <summary>
    ///     樹種の根元設定をguidで引くマップと、そこから派生する切り出しhalo・splatmapの列を検証する。
    ///     マップの規約はTreeHeightModifier.BuildGuidModMapと揃っていなければならず、
    ///     haloが幅を下回るとタイル境界の外の木が落ちて根元の塗りが境界で直線に切れる
    ///     Checks the guid-keyed map of per-species root settings and the slice halo and splatmap columns derived from it.
    ///     The map must follow the same rule as TreeHeightModifier.BuildGuidModMap, and a halo below the configured width
    ///     drops trees past the tile edge, breaking the root patches in a straight line at the seam
    /// </summary>
    public class TreeSurroundGuidMapTest
    {
        private const string TreeGuid = "11111111-1111-1111-1111-111111111111";
        private const string SecondTreeGuid = "33333333-3333-3333-3333-333333333333";
        private const string RockGuid = "22222222-2222-2222-2222-222222222222";

        [Test]
        public void TheFirstPrototypeListingAGuidWinsAndDisabledOnesAreSkipped()
        {
            // TreeHeightModifier.BuildGuidModMapと同じ規約。後勝ちにすると同じguidで高さと根元が別プロトタイプを向く
            // The same rule as BuildGuidModMap; letting the last win would point one guid's height and root at different prototypes
            var config = CreateConfig();
            config.grassland.treePlacement = new TreePlacementConfig
            {
                prototypes = new[]
                {
                    CreateEntry(new[] { string.Empty, TreeGuid }, "addr/first", 1f, 10f),
                    CreateEntry(new[] { TreeGuid, SecondTreeGuid }, "addr/second", 1f, 20f),
                    CreateDisabledEntry(new[] { RockGuid }, "addr/disabled", 1f, 30f),
                },
            };

            var surroundParamsByGuid = TreeSurroundTexturePainter.BuildGuidSurroundMap(
                new BiomePlacementHelper(config), new[] { BiomeType.Grassland });

            Assert.That(surroundParamsByGuid[TreeGuid].layerAddress, Is.EqualTo("addr/first"));
            Assert.That(surroundParamsByGuid[TreeGuid].width, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(surroundParamsByGuid[SecondTreeGuid].layerAddress, Is.EqualTo("addr/second"));
            Assert.That(surroundParamsByGuid.ContainsKey(RockGuid), Is.False);
            Assert.That(surroundParamsByGuid.ContainsKey(string.Empty), Is.False);
        }

        [Test]
        public void ANonPaintingPrototypeStillOwnsItsGuid()
        {
            // 塗らないプロトタイプを載せずに飛ばすと、同じguidを持つ後続が勝ってしまい規約が崩れる
            // Skipping a non-painting prototype would let a later one holding the same guid win and break the rule
            var config = CreateConfig();
            config.grassland.treePlacement = new TreePlacementConfig
            {
                prototypes = new[]
                {
                    CreateEntry(new[] { TreeGuid }, string.Empty, 1f, 10f),
                    CreateEntry(new[] { TreeGuid }, "addr/second", 1f, 20f),
                },
            };

            var surroundParamsByGuid = TreeSurroundTexturePainter.BuildGuidSurroundMap(
                new BiomePlacementHelper(config), new[] { BiomeType.Grassland });

            Assert.That(surroundParamsByGuid[TreeGuid].layerAddress, Is.Empty);
        }

        [Test]
        public void TheReachAndTheReservedLayersCoverOnlyThePaintingPrototypes()
        {
            // 塗らない樹種まで数えるとhaloが無駄に広がり、使われないTerrainLayerの列まで確保される
            // Counting non-painting species would widen the halo for nothing and reserve unused TerrainLayer columns
            var surroundParamsByGuid = new Dictionary<string, (string layerAddress, float weight, float width)>
            {
                { TreeGuid, ("addr/Mud01", 0.8f, 12f) },
                { SecondTreeGuid, (string.Empty, 0.8f, 40f) },
                { RockGuid, ("addr/unused", 0f, 80f) },
            };

            Assert.That(TreeSurroundTexturePainter.MaxReach(surroundParamsByGuid), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(TreeSurroundTexturePainter.LayerAddresses(surroundParamsByGuid),
                Is.EqualTo(new[] { "addr/Mud01" }));
        }

        private static TreePrototypeEntry CreateEntry(
            string[] mapObjectGuids, string layerAddress, float weight, float width)
        {
            return new TreePrototypeEntry
            {
                mapObjectGuids = mapObjectGuids,
                surroundLayerAddressablePath = layerAddress,
                surroundLayerWeight = weight,
                surroundLayerWidth = width,
            };
        }

        private static TreePrototypeEntry CreateDisabledEntry(
            string[] mapObjectGuids, string layerAddress, float weight, float width)
        {
            var entry = CreateEntry(mapObjectGuids, layerAddress, weight, width);
            entry.disabled = true;
            return entry;
        }
    }
}
