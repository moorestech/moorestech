using System.IO;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Tests.Module;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration.Facade
{
    // template/generated双方の結果契約を検証
    // Verifies the result contract for both template and generated worlds
    // 通常1x1、全タイル走査だけ低解像度2x2
    // Keep ordinary real generation at 1x1 and use a dedicated low-resolution 2x2 only for all-tile traversal
    public class WorldTerrainSessionTest
    {
        // templateは固定地形の結果のみ返す
        // A template world returns only the authored result
        [Test]
        public void TemplateOpensAsTerrainAssetLayout()
        {
            var session = WorldTerrainSession.Open(
                TerrainTransferMeta.CreateTemplate("0123456789abcdef", 0), TestModDirectory.ForUnitTestModDirectory);
            Assert.That(session.Layout.Kind, Is.EqualTo(TerrainLayoutKind.TerrainAsset));
            Assert.That(session.Layout.AuthoredTerrainDataAddress, Is.EqualTo("Vanilla/Environment/TemplateTerrainData"));
            Assert.That(session.Layout.TileCoordinates, Is.Empty);

            // 焼く口を持つのはTiledTerrainSessionだけ。実行時throwではなく型で焼けないことを表す
            // Only TiledTerrainSession exposes baking; the type, not a runtime throw, states that this session cannot bake
            Assert.That(session, Is.Not.InstanceOf<TiledTerrainSession>());
        }

        // generatedはプロビジョニング済みメタから開き全タイルが寸法通り返る
        // A generated world opens from a provisioned meta and every tile returns results of the declared dimensions
        [Test]
        public void GeneratedWorldBakesEveryTile()
        {
            const int MultiTileGridSide = 2;
            const int MultiTileResolution = 129;
            var scope = new TerrainTransferTestScope(nameof(GeneratedWorldBakesEveryTile));
            try
            {
                var worldDirectory = scope.ProvisionGeneratedWorld(5, MultiTileGridSide, MultiTileResolution);
                var meta = TerrainTransferMetaReader.Read(worldDirectory);
                var shared = WorldDataDirectory.ForWorldCache(meta.WorldId);

                // 転送後と同じ高さ源で全4座標を焼く
                // Copy heights into the shared cache and bake all four coordinates in the post-transfer state
                TerrainTransferTestScope.CopyDirectory(worldDirectory.TerrainDirectory, shared.TerrainDirectory);
                try
                {
                    var session = WorldTerrainSession.Open(meta, TestModDirectory.ForUnitTestModDirectory);
                    Assert.That(session.Layout.Kind, Is.EqualTo(TerrainLayoutKind.TileMaps));
                    Assert.That(session.Layout.HeightmapResolution, Is.EqualTo(MultiTileResolution));
                    Assert.That(session.Layout.TileCoordinates, Is.EquivalentTo(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }));

                    // 各座標の焼成出力寸法を検証
                    // Open the generated world as its bakeable type and verify each coordinate's output dimensions
                    var tiledSession = (TiledTerrainSession)session;
                    foreach (var (x, z) in session.Layout.TileCoordinates)
                    {
                        var tile = tiledSession.BakeTile(x, z);
                        Assert.That(tile.DisplayHeights.GetLength(0), Is.EqualTo(MultiTileResolution));
                        Assert.That(tile.Alphamap.GetLength(2), Is.EqualTo(session.Layout.TextureLayerAddresses.Count));
                        Assert.That(tile.DetailMaps.Count, Is.EqualTo(session.Layout.DetailPrototypes.Count));
                    }
                }
                finally
                {
                    if (Directory.Exists(shared.Root)) Directory.Delete(shared.Root, true);
                }
            }
            finally
            {
                scope.End();
            }
        }
    }
}
