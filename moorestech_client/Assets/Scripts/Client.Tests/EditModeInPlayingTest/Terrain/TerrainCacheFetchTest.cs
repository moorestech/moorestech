using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Starter.Initialization;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEditor;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// generatedワールドで起動し、地形バイナリがローカルキャッシュへ復元・再利用・再取得されることを検証する
    /// Boots a generated world and verifies the terrain binaries are restored, reused, and re-fetched in the local cache
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
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
                await LoadMainGameWithMapMode(null, worldDirectory, WorldMapMode.Generated);

                var mapLayout = await ClientContext.VanillaApi.Response.GetMapData(default);
                Assert.AreEqual(WorldMapMode.Generated, mapLayout.TerrainMeta.MapMode, "generatedモードで起動していない");
                Assert.Less(0, mapLayout.TerrainMeta.TerrainChunkTotal, "地形チャンクが1本も無い");

                var terrainMeta = mapLayout.TerrainMeta.ToTerrainTransferMeta();
                var cacheWorldDirectory = WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(terrainMeta.WorldId));
                var segments = TerrainTransferMeta
                    .EnumerateStreamSegments(cacheWorldDirectory, terrainMeta.TerrainTileCount, terrainMeta.TerrainResolution).ToList();

                // ① 起動時のフェッチでキャッシュに元のファイル名・想定バイト長・同一内容で復元されている
                // (1) The boot-time fetch restored the cache with the original file names, expected byte lengths, and identical content
                AssertAllSegmentsRestored(segments);
                Assert.AreEqual(mapLayout.TerrainMeta.TerrainHash, TerrainStreamHasher.Compute(cacheWorldDirectory, terrainMeta), "復元内容がサーバーの地形と一致しない");

                // ② 最新キャッシュでは取得しない
                // (2) Fetch nothing for a fresh cache
                var terrainDataFetcher = new TerrainDataFetcher(ClientContext.VanillaApi.Response);
                var reuseFetchedCount = await terrainDataFetcher.RunAsync(mapLayout);
                Assert.AreEqual(0, reuseFetchedCount, "キャッシュヒットなのにチャンクを取得した");

                // ③ キャッシュファイルを1個破損させると全チャンクを取り直して壊したバイトまで元に戻す
                // (3) Corrupting one cached file forces a full re-fetch that restores even the broken byte
                var originalFirstByte = CorruptFile(segments[0].FilePath);
                var refetchedCount = await terrainDataFetcher.RunAsync(mapLayout);
                Assert.AreEqual(terrainMeta.TerrainChunkTotal, refetchedCount, "破損検知後に全チャンクを取り直していない");
                AssertAllSegmentsRestored(segments);
                Assert.AreEqual(originalFirstByte, File.ReadAllBytes(segments[0].FilePath)[0], "破損させたバイトが復元されていない");
                Assert.AreEqual(mapLayout.TerrainMeta.TerrainHash, TerrainStreamHasher.Compute(cacheWorldDirectory, terrainMeta), "再取得後の内容がサーバーの地形と一致しない");

                // ④ 届いたバイトがサーバー申告のハッシュと食い違えば例外にする。転送破損をキャッシュヒットとして持ち越さない
                // (4) Bytes disagreeing with the hash the server declared must throw, never carry transfer corruption forward as a cache hit
                var tamperedLayout = new GetMapDataProtocol.ResponseMapDataMessagePack(
                    mapLayout.Spawn, mapLayout.MapObjects, mapLayout.MapVeins, terrainMeta, new string('0', mapLayout.TerrainMeta.TerrainHash.Length));
                var tamperedFetchException = await CaptureFetchException(terrainDataFetcher, tamperedLayout);
                Assert.IsNotNull(tamperedFetchException, "申告ハッシュと不一致なのに取得が成功扱いになった");
                StringAssert.Contains("does not match the server hash", tamperedFetchException.Message);

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

            // 先頭バイトを別の値に書き換えてハッシュ不一致を起こし、復元確認用に元の値を返す
            // Rewrite the first byte with a different value to trigger a hash mismatch, returning the original for the restore check
            byte CorruptFile(string filePath)
            {
                var fileBytes = File.ReadAllBytes(filePath);
                var originalByte = fileBytes[0];
                fileBytes[0] = (byte)(originalByte ^ 0xFF);
                File.WriteAllBytes(filePath, fileBytes);
                return originalByte;
            }

            async UniTask<Exception> CaptureFetchException(TerrainDataFetcher terrainDataFetcher, GetMapDataProtocol.ResponseMapDataMessagePack layout)
            {
                // 非同期例外の捕捉は自前で行う。NUnitのAssert.ThrowsAsyncはUniTaskの継続を回すPlayerLoopごと止めてしまう
                // Capture the async exception by hand: NUnit's Assert.ThrowsAsync would block the very PlayerLoop that drives UniTask continuations
                try
                {
                    await terrainDataFetcher.RunAsync(layout);
                }
                catch (InvalidOperationException fetchException)
                {
                    return fetchException;
                }
                return null;
            }

            #endregion
        }
    }
}
