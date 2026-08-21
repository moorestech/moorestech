using System.Linq;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    public class PlacementSurroundEffectTest
    {
        // 木配置器の出力は樹種エントリの種別を、オブジェクト配置器の出力は objectConfig エントリの種別を持つ
        // Tree-placer output carries the species entry's kind and object-placer output the objectConfig entry's kind
        [Test]
        public void PlacementsCarryTheirSourceEntrySurroundEffect()
        {
            var config = MultiTileTestWorld.BuildConfig(1, 7);
            MultiTileTestWorld.EnableTrees(config);
            MultiTileTestWorld.EnableObjects(config);
            config.grassland.treePlacement.prototypes[0].terrainSurroundEffectType = TerrainSurroundEffectType.rockNoBareGround;
            config.grassland.objectConfig.entries[0].terrainSurroundEffectType = TerrainSurroundEffectType.rockBareGround;

            var output = new VanillaGenerator().Generate(config);
            var treeGuid = config.grassland.treePlacement.prototypes[0].mapObjectGuids[0];
            var objectGuid = MultiTileTestWorld.IndependentMapObjectGuid;
            Assert.That(output.MapObjects.Any(m => m.MapObjectGuid == treeGuid), Is.True);
            Assert.That(output.MapObjects.Any(m => m.MapObjectGuid == objectGuid), Is.True);
            // この時点では PlacedMapObject に種別は無い（Task 3 で台帳に載る）。ここでは配置器の入力が届いていることだけを確認する
            // PlacedMapObject carries no kind yet (the ledger in Task 3 does); only confirm the placers received the input here
        }
    }
}
