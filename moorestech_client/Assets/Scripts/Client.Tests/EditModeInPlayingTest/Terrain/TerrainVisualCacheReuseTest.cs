using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.Context;
using Client.Game.InGame.Environment.Terrain;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// generatedワールドで起動し、2回目の地形構築がsplatmap/detailの再生成を丸ごと省くことを検証する
    /// Boots a generated world and verifies a second terrain build skips the whole splatmap and detail regeneration
    /// </summary>
    public class TerrainVisualCacheReuseTest
    {
        [UnityTest]
        public IEnumerator SecondBuildReusesEveryTileFromTheVisualCache()
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
                var worldDirectory = Path.Combine(Path.GetTempPath(), $"moorestech_terrain_visual_cache_test_{Guid.NewGuid()}");
                await LoadMainGameWithMapMode(null, worldDirectory, WorldProvisioner.GeneratedMapMode);

                var mapLayout = await ClientContext.VanillaApi.Response.GetMapData(default);
                Assert.AreEqual(WorldProvisioner.GeneratedMapMode, mapLayout.TerrainMeta.MapMode, "generatedモードで起動していない");
                Assert.Less(0, mapLayout.TerrainMeta.TerrainTileCount, "地形タイルが1枚も無い");
                var cacheWorldDirectory = WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(mapLayout.TerrainMeta.WorldId));

                // 起動処理は地形構築の完了を待たずに戻る。全ファイルを待ってから、このテスト固有worldIdのvisualだけを空にする
                // The boot returns before terrain construction; wait for every file, then empty only this test worldId's visual cache
                await UniTask.WaitUntil(AreAllVisualCacheFilesWritten).Timeout(TimeSpan.FromSeconds(60));
                Directory.Delete(cacheWorldDirectory.TerrainVisualDirectory, true);
                Assert.IsFalse(Directory.Exists(cacheWorldDirectory.TerrainVisualDirectory), "テスト用visualキャッシュが空でない");

                // ① 空のtest固有cacheで第1構築を完了させ、取り逃し数0を正確なRuntimeBuilderログで確認する
                // (1) Build first against the empty test-specific cache and verify the exact zero-hit RuntimeBuilder log
                await BuildWithExpectedCacheHits(0, "TerrainVisualCacheFirstBuild");
                foreach (var tile in TerrainTransferMeta.EnumerateTileCoordinates(mapLayout.TerrainMeta.TerrainTileCount))
                    FileAssert.Exists(cacheWorldDirectory.TerrainVisualCacheFilePath(tile.TileX, tile.TileZ));

                // ② 第2構築では全タイルがhitする。全数hitならsplatmap/detailの再生成を一切通らない
                // (2) The second build must hit every tile; a full hit count means it never regenerates splatmaps or details
                await BuildWithExpectedCacheHits(mapLayout.TerrainMeta.TerrainTileCount, "TerrainVisualCacheSecondBuild");

                // ③ 対照: キャッシュファイルを消せば取り逃す。②が常にtrueを返すだけの検査でないことを示す
                // (3) Control: deleting the file misses, showing (2) is not merely an assertion that always holds
                var firstTileCacheFilePath = cacheWorldDirectory.TerrainVisualCacheFilePath(0, 0);
                File.Delete(firstTileCacheFilePath);
                Assert.IsFalse(await CreateTileAndReportCacheHit(0, 0), "キャッシュが無いのにヒット扱いになった");
                FileAssert.Exists(firstTileCacheFilePath);

                Directory.Delete(cacheWorldDirectory.Root, true);
                Directory.Delete(worldDirectory, true);

                bool AreAllVisualCacheFilesWritten()
                {
                    foreach (var tile in TerrainTransferMeta.EnumerateTileCoordinates(mapLayout.TerrainMeta.TerrainTileCount))
                        if (!File.Exists(cacheWorldDirectory.TerrainVisualCacheFilePath(tile.TileX, tile.TileZ)))
                            return false;

                    return true;
                }

                async UniTask BuildWithExpectedCacheHits(int expectedCacheHits, string rootName)
                {
                    var buildRoot = new GameObject(rootName);
                    LogAssert.Expect(LogType.Log, new Regex(
                        $@"\[TerrainRuntimeBuilder\] Generated terrain built: tiles={mapLayout.TerrainMeta.TerrainTileCount} visualCacheHits={expectedCacheHits} elapsedMs=\d+"));
                    await TerrainRuntimeBuilder.BuildAsync(mapLayout, buildRoot.transform);
                    UnityEngine.Object.DestroyImmediate(buildRoot);
                }
            }

            // 起動時と同じ入口からタイルを1枚組み立て、見た目を再生成せずに済んだかを返す
            // Builds one tile through the same entry point the boot uses and reports whether the visuals were reused
            async UniTask<bool> CreateTileAndReportCacheHit(int tileX, int tileZ)
            {
                var freshMapLayout = await ClientContext.VanillaApi.Response.GetMapData(default);
                var wireMeta = freshMapLayout.TerrainMeta;
                var terrainSource = await GeneratedTerrainSource.CreateAsync(
                    wireMeta.ToTerrainTransferMeta(), wireMeta.TerrainHash, freshMapLayout.MapObjects);
                var (terrainData, visualCacheHit) = await terrainSource.CreateTerrainDataAsync(tileX, tileZ);

                UnityEngine.Object.DestroyImmediate(terrainData);
                return visualCacheHit;
            }

            #endregion
        }
    }
}
