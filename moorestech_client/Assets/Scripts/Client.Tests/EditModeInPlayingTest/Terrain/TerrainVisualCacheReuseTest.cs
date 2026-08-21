using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

                // ① 空のtest固有cacheで第1構築を完了させ、全タイルぶんのキャッシュファイルが書かれることを確認する
                // (1) Build first against the empty test-specific cache and verify every tile's cache file gets written
                await BuildAsync("TerrainVisualCacheFirstBuild");
                var writeTimesAfterFirstBuild = new Dictionary<(int TileX, int TileZ), DateTime>();
                foreach (var tile in TerrainTransferMeta.EnumerateTileCoordinates(mapLayout.TerrainMeta.TerrainTileCount))
                {
                    var filePath = cacheWorldDirectory.TerrainVisualCacheFilePath(tile.TileX, tile.TileZ);
                    FileAssert.Exists(filePath);
                    writeTimesAfterFirstBuild[(tile.TileX, tile.TileZ)] = File.GetLastWriteTimeUtc(filePath);
                }

                // ② 第2構築は全タイルをキャッシュから読むだけで済む。書き戻しはヒットしたタイルでは起きないので、更新時刻が一切動かない
                // (2) The second build reads every tile from the cache alone; a hit never writes back, so no file's timestamp moves
                await BuildAsync("TerrainVisualCacheSecondBuild");
                foreach (var tile in TerrainTransferMeta.EnumerateTileCoordinates(mapLayout.TerrainMeta.TerrainTileCount))
                {
                    var filePath = cacheWorldDirectory.TerrainVisualCacheFilePath(tile.TileX, tile.TileZ);
                    Assert.That(File.GetLastWriteTimeUtc(filePath), Is.EqualTo(writeTimesAfterFirstBuild[(tile.TileX, tile.TileZ)]),
                        $"tile ({tile.TileX}, {tile.TileZ}) was rewritten on what should have been a full cache hit");
                }

                // ③ 対照: キャッシュファイルを消せば取り逃し、再構築時に新しいファイルが書かれる。②が常にtrueを返すだけの検査でないことを示す
                // (3) Control: deleting the file misses, and rebuilding writes a fresh one, showing (2) is not merely an assertion that always holds
                var firstTileCacheFilePath = cacheWorldDirectory.TerrainVisualCacheFilePath(0, 0);
                File.Delete(firstTileCacheFilePath);
                Assert.IsFalse(File.Exists(firstTileCacheFilePath), "キャッシュファイルの削除に失敗した");
                await CreateTileAsync(0, 0);
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

                async UniTask BuildAsync(string rootName)
                {
                    var buildRoot = new GameObject(rootName);
                    await TerrainRuntimeBuilder.BuildAsync(mapLayout, buildRoot.transform);
                    UnityEngine.Object.DestroyImmediate(buildRoot);
                }
            }

            // 起動時と同じ入口からタイルを1枚組み立てる。見た目の再生成有無はキャッシュファイルの更新時刻で確認する
            // Builds one tile through the same entry point the boot uses; whether the visuals were regenerated is checked via the cache file's timestamp
            async UniTask CreateTileAsync(int tileX, int tileZ)
            {
                var freshMapLayout = await ClientContext.VanillaApi.Response.GetMapData(default);
                var wireMeta = freshMapLayout.TerrainMeta;
                var terrainSource = await GeneratedTerrainSource.CreateAsync(
                    wireMeta.ToTerrainTransferMeta(), wireMeta.TerrainHash, freshMapLayout.MapObjects);
                var terrainData = await terrainSource.CreateTerrainDataAsync(tileX, tileZ);

                UnityEngine.Object.DestroyImmediate(terrainData);
            }

            #endregion
        }
    }
}
