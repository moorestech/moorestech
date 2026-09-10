using System.IO;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Tests.Module;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration.Facade
{
    // ワールド生成直後の共有キャッシュ先焼き(TerrainVisualPrebake)を検証する
    // ProvisionGeneratedWorld呼び出しだけで検証対象が走る
    // Verifies the shared-cache prebake (TerrainVisualPrebake) that runs right after world generation
    // Calling ProvisionGeneratedWorld alone already exercises the target
    // 全タイル走査だけ低解像度2x2で検証
    // Keep ordinary generation at 1x1; only the all-tiles traversal switches to the low-resolution 2x2 master
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardServerMap2")]
    public class TerrainVisualPrebakeTest
    {
        [Test]
        public void 生成ワールドの先焼きで共有キャッシュへ全タイルの見た目ファイルが書き出される()
        {
            const int expectedTileCount =
                TerrainTransferTestScope.LowResolutionMultiTileGridSide * TerrainTransferTestScope.LowResolutionMultiTileGridSide;
            var scope = new TerrainTransferTestScope(nameof(生成ワールドの先焼きで共有キャッシュへ全タイルの見た目ファイルが書き出される));
            try
            {
                var worldDirectory = scope.ProvisionLowResolutionMultiTileGeneratedWorld(777);
                var meta = (GeneratedTerrainTransferMeta)TerrainTransferMetaReader.Read(worldDirectory);
                var shared = WorldDataDirectory.ForWorldCache(meta.WorldId);

                Assert.AreEqual(expectedTileCount, meta.TerrainTileCount);
                foreach (var (tileX, tileZ) in TerrainTransferMeta.EnumerateTileCoordinates(meta.TerrainTileCount))
                    Assert.IsTrue(File.Exists(shared.TerrainVisualCacheFilePath(tileX, tileZ)),
                        $"tile ({tileX},{tileZ}) should have been prebaked into the shared cache");
            }
            finally
            {
                // 共有キャッシュもワールドもEndが唯一の削除主体
                // End alone owns deleting both the shared cache and the world, even when provisioning fails partway through
                scope.End();
            }
        }

        // pass-1(配置台帳の再生成)はOpenのたびに走るが、pass-2(splat/detail)は先焼き済みキャッシュを引く
        // 見た目ファイルのmtime不変で検証
        // pass-1 (ledger regeneration) runs on every Open, but pass-2 (splat/detail) hits the prebaked cache
        // Verified by the visual file's mtime staying put
        [Test]
        public void 先焼き済みキャッシュはOpen_BakeTileで再構築されずmtimeが変わらない()
        {
            var scope = new TerrainTransferTestScope(nameof(先焼き済みキャッシュはOpen_BakeTileで再構築されずmtimeが変わらない));
            var worldDirectory = scope.ProvisionGeneratedWorld(778);
            var meta = (GeneratedTerrainTransferMeta)TerrainTransferMetaReader.Read(worldDirectory);
            var shared = WorldDataDirectory.ForWorldCache(meta.WorldId);
            try
            {
                var cacheFilePath = shared.TerrainVisualCacheFilePath(0, 0);
                Assert.IsTrue(File.Exists(cacheFilePath));
                var prebakeWriteTime = File.GetLastWriteTimeUtc(cacheFilePath);

                // Openの高さ源は共有キャッシュなので、転送後と同じ状態を作る: world dir の terrain/ を共有キャッシュへ複製
                // Open reads heights from the shared cache, so replicate the post-transfer state: copy the world dir's terrain/ into the shared cache
                TerrainTransferTestScope.CopyDirectory(worldDirectory.TerrainDirectory, shared.TerrainDirectory);

                var session = (TiledTerrainSession)WorldTerrainSession.Open(meta, TestModDirectory.ForUnitTestModDirectory);
                session.BakeTile(0, 0);

                var afterOpenWriteTime = File.GetLastWriteTimeUtc(cacheFilePath);
                Assert.AreEqual(prebakeWriteTime, afterOpenWriteTime,
                    "pass-2 must hit the prebaked cache instead of rebuilding and rewriting it");
            }
            finally
            {
                scope.End();
            }
        }
    }
}
