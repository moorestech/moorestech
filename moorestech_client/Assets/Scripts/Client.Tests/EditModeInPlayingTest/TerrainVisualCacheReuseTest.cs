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
                Assert.AreEqual(WorldProvisioner.GeneratedMapMode, mapLayout.MapMode, "generatedモードで起動していない");
                Assert.Less(0, mapLayout.TerrainTileCount, "地形タイルが1枚も無い");
                var cacheWorldDirectory = WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(mapLayout.WorldId));

                // 起動処理は地形構築の完了を待たずに戻る。既存Terrainは数えず、今回の全キャッシュファイルを待つ
                // The boot returns before terrain construction; wait for this build's cache files, never count pre-existing Terrains
                await UniTask.WaitUntil(AreAllVisualCacheFilesWritten).Timeout(TimeSpan.FromSeconds(60));

                // ① 起動時の構築で全タイルぶんの見た目キャッシュが書き出されている
                // (1) The boot-time build wrote a visual cache file for every tile
                foreach (var tile in TerrainTransferMeta.EnumerateTileCoordinates(mapLayout.TerrainTileCount))
                    FileAssert.Exists(cacheWorldDirectory.TerrainVisualCacheFilePath(tile.TileX, tile.TileZ));

                // ② 2回目の構築ログで全タイルがヒットしたことを確認する。ヒット数が全数ならsplatmapもdetailも再生成していない
                // (2) The second build log must show every tile hit; a full hit count means neither splatmaps nor details regenerated
                var secondBuildRoot = new GameObject("TerrainVisualCacheSecondBuild");
                LogAssert.Expect(LogType.Log, new Regex(
                    $@"\[TerrainRuntimeBuilder\] Generated terrain built: tiles={mapLayout.TerrainTileCount} visualCacheHits={mapLayout.TerrainTileCount} elapsedMs=\d+"));
                await TerrainRuntimeBuilder.BuildAsync(mapLayout, secondBuildRoot.transform);
                UnityEngine.Object.DestroyImmediate(secondBuildRoot);

                // ③ 対照: キャッシュファイルを消せば取り逃す。②が常にtrueを返すだけの検査でないことを示す
                // (3) Control: deleting the file misses, showing (2) is not merely an assertion that always holds
                var firstTileCacheFilePath = cacheWorldDirectory.TerrainVisualCacheFilePath(0, 0);
                File.Delete(firstTileCacheFilePath);
                Assert.IsFalse(await CreateTileAndReportCacheHit(0, 0), "キャッシュが無いのにヒット扱いになった");
                FileAssert.Exists(firstTileCacheFilePath);

                // 検証で汚したキャッシュとワールドを片付ける
                // Clean up the cache and world this test created
                Directory.Delete(cacheWorldDirectory.Root, true);
                Directory.Delete(worldDirectory, true);

                bool AreAllVisualCacheFilesWritten()
                {
                    foreach (var tile in TerrainTransferMeta.EnumerateTileCoordinates(mapLayout.TerrainTileCount))
                        if (!File.Exists(cacheWorldDirectory.TerrainVisualCacheFilePath(tile.TileX, tile.TileZ)))
                            return false;

                    return true;
                }
            }

            // 起動時と同じ入口からタイルを1枚組み立て、見た目を再生成せずに済んだかを返す
            // Builds one tile through the same entry point the boot uses and reports whether the visuals were reused
            async UniTask<bool> CreateTileAndReportCacheHit(int tileX, int tileZ)
            {
                var mapLayout = await ClientContext.VanillaApi.Response.GetMapData(default);
                var terrainSource = await GeneratedTerrainSource.CreateAsync(mapLayout);
                var terrainData = terrainSource.CreateTerrainData(tileX, tileZ, out var visualCacheHit);

                UnityEngine.Object.DestroyImmediate(terrainData);
                return visualCacheHit;
            }

            #endregion
        }
    }
}
