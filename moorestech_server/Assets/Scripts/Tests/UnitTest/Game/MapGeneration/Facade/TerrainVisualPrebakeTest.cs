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
    // 全タイル契約は1タイルでも成立する。速度回帰を避けるためテストmasterの1x1を拡大しない
    // The all-tiles contract holds with one tile; do not enlarge the test master's 1x1 and regress test time
    public class TerrainVisualPrebakeTest
    {
        [Test]
        public void 生成ワールドの先焼きで共有キャッシュへ全タイルの見た目ファイルが書き出される()
        {
            var scope = new TerrainTransferTestScope(nameof(生成ワールドの先焼きで共有キャッシュへ全タイルの見た目ファイルが書き出される));
            var worldDirectory = scope.ProvisionGeneratedWorld(777);
            var meta = TerrainTransferMetaReader.Read(worldDirectory);
            var shared = WorldDataDirectory.ForWorldCache(meta.WorldId);
            try
            {
                foreach (var (tileX, tileZ) in TerrainTransferMeta.EnumerateTileCoordinates(meta.TerrainTileCount))
                    Assert.IsTrue(File.Exists(shared.TerrainVisualCacheFilePath(tileX, tileZ)),
                        $"tile ({tileX},{tileZ}) should have been prebaked into the shared cache");
            }
            finally
            {
                Directory.Delete(shared.Root, true);
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
            var meta = TerrainTransferMetaReader.Read(worldDirectory);
            var shared = WorldDataDirectory.ForWorldCache(meta.WorldId);
            try
            {
                var cacheFilePath = shared.TerrainVisualCacheFilePath(0, 0);
                Assert.IsTrue(File.Exists(cacheFilePath));
                var prebakeWriteTime = File.GetLastWriteTimeUtc(cacheFilePath);

                // Openの高さ源は共有キャッシュなので、転送後と同じ状態を作る: world dir の terrain/ を共有キャッシュへ複製
                // Open reads heights from the shared cache, so replicate the post-transfer state: copy the world dir's terrain/ into the shared cache
                CopyDirectory(worldDirectory.TerrainDirectory, shared.TerrainDirectory);

                var session = (TiledTerrainSession)WorldTerrainSession.Open(meta, TestModDirectory.ForUnitTestModDirectory);
                session.BakeTile(0, 0);

                var afterOpenWriteTime = File.GetLastWriteTimeUtc(cacheFilePath);
                Assert.AreEqual(prebakeWriteTime, afterOpenWriteTime,
                    "pass-2 must hit the prebaked cache instead of rebuilding and rewriting it");
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
