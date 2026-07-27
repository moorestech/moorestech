using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Starter.Initialization;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// generatedワールドで起動し、地形バイナリがローカルキャッシュへ復元・再利用・再取得されることを検証する
    /// Boots a generated world and verifies the terrain binaries are restored, reused, and re-fetched in the local cache
    /// </summary>
    public class TerrainCacheFetchTest
    {
        [UnityTest]
        public IEnumerator TerrainCacheRestoreReuseAndRefetchTest()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);

            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                var worldDirectory = Path.Combine(Path.GetTempPath(), $"moorestech_terrain_cache_test_{Guid.NewGuid()}");
                await LoadMainGameWithMapMode(null, worldDirectory, WorldProvisioner.GeneratedMapMode);

                var mapLayout = await ClientContext.VanillaApi.Response.GetMapData(default);
                Assert.AreEqual(WorldProvisioner.GeneratedMapMode, mapLayout.MapMode, "generatedモードで起動していない");
                Assert.Less(0, mapLayout.TerrainChunkTotal, "地形チャンクが1本も無い");

                var cacheWorldDirectory = WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(mapLayout.WorldId));
                var segments = TerrainTransferMeta
                    .EnumerateStreamSegments(cacheWorldDirectory, mapLayout.TerrainTileCount, mapLayout.TerrainResolution).ToList();

                // ① 起動時のフェッチでキャッシュに元のファイル名・想定バイト長で復元されている
                // (1) The boot-time fetch restored the cache with the original file names and expected byte lengths
                AssertAllSegmentsRestored(segments);

                // ② キャッシュが最新なら1チャンクも取得しない
                // (2) A up-to-date cache fetches zero chunks
                var terrainDataFetcher = new TerrainDataFetcher(ClientContext.VanillaApi.Response);
                var reuseFetchedCount = await terrainDataFetcher.RunAsync(mapLayout);
                Assert.AreEqual(0, reuseFetchedCount, "キャッシュヒットなのにチャンクを取得した");

                // ③ キャッシュファイルを1個破損させると全チャンクを取り直して内容を復元する
                // (3) Corrupting one cached file forces a full re-fetch that restores the content
                CorruptFile(segments[0].FilePath);
                var refetchedCount = await terrainDataFetcher.RunAsync(mapLayout);
                Assert.AreEqual(mapLayout.TerrainChunkTotal, refetchedCount, "破損検知後に全チャンクを取り直していない");
                AssertAllSegmentsRestored(segments);

                // 検証で汚したキャッシュとワールドを片付ける
                // Clean up the cache and world this test created
                Directory.Delete(cacheWorldDirectory.Root, true);
                Directory.Delete(worldDirectory, true);
            }

            void AssertAllSegmentsRestored(List<(string FilePath, long ByteLength)> segments)
            {
                foreach (var segment in segments)
                {
                    FileAssert.Exists(segment.FilePath);
                    Assert.AreEqual(segment.ByteLength, new FileInfo(segment.FilePath).Length, $"復元サイズが想定と違う: {segment.FilePath}");
                }
            }

            // 先頭バイトを別の値に書き換えて、ハッシュ不一致を起こす
            // Rewrite the first byte with a different value to trigger a hash mismatch
            void CorruptFile(string filePath)
            {
                var fileBytes = File.ReadAllBytes(filePath);
                fileBytes[0] = (byte)(fileBytes[0] ^ 0xFF);
                File.WriteAllBytes(filePath, fileBytes);
            }

            #endregion
        }
    }
}
