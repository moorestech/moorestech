using System;
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
            Assert.Throws<InvalidOperationException>(() => session.BakeTile(0, 0));
        }

        // generatedはプロビジョニング済みメタから開き全タイルが寸法通り返る
        // A generated world opens from a provisioned meta and every tile returns results of the declared dimensions
        [Test]
        public void GeneratedWorldBakesEveryTile()
        {
            var scope = new TerrainTransferTestScope(nameof(GeneratedWorldBakesEveryTile));
            var worldDirectory = scope.ProvisionGeneratedWorld(5);
            var meta = TerrainTransferMetaReader.Read(worldDirectory);

            // 高さ源は共有キャッシュなので、転送後と同じ状態を作る: world dir の terrain/ を cache/worlds/<id>/terrain へ複製
            // The height source is the shared cache, so replicate the post-transfer state: copy the world dir's terrain/ into cache/worlds/<id>/terrain
            var shared = WorldDataDirectory.ForWorldCache(meta.WorldId);
            CopyDirectory(worldDirectory.TerrainDirectory, shared.TerrainDirectory);
            try
            {
                var session = WorldTerrainSession.Open(meta, TestModDirectory.ForUnitTestModDirectory);
                Assert.That(session.Layout.Kind, Is.EqualTo(TerrainLayoutKind.TileMaps));
                foreach (var (x, z) in session.Layout.TileCoordinates)
                {
                    var tile = session.BakeTile(x, z);
                    Assert.That(tile.DisplayHeights.GetLength(0), Is.EqualTo(session.Layout.HeightmapResolution));
                    Assert.That(tile.Alphamap.GetLength(2), Is.EqualTo(session.Layout.TextureLayerAddresses.Count));
                    Assert.That(tile.DetailMaps.Count, Is.EqualTo(session.Layout.DetailPrototypes.Count));
                }
            }
            finally
            {
                Directory.Delete(shared.Root, true);
                scope.End();
            }
        }

        // テストが払い出した実ディレクトリ間だけを想定した単純な再帰コピー。シンボリックリンクは扱わない
        // A simple recursive copy meant only for real directories this test hands out; symlinks are not handled
        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (var filePath in Directory.GetFiles(sourceDirectory))
                File.Copy(filePath, Path.Combine(destinationDirectory, Path.GetFileName(filePath)), true);
            foreach (var subDirectory in Directory.GetDirectories(sourceDirectory))
                CopyDirectory(subDirectory, Path.Combine(destinationDirectory, Path.GetFileName(subDirectory)));
        }
    }
}
