using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Server.Protocol.PacketResponse;
using UnityEngine;

// System.Diagnostics を丸ごと開くと Debug が UnityEngine.Debug と衝突する
// Opening all of System.Diagnostics would collide Debug with UnityEngine.Debug
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Client.Game.InGame.Environment.Terrain
{
    /// <summary>
    ///     ワールドの地形をシーンへ建てる唯一の入口。generatedは転送バイナリから組み立て、templateは
    ///     オーサリング済みTerrainDataを載せるだけで、どちらも同じTerrain GameObjectとして生える
    ///     The single entry point standing a world's terrain up in the scene; generated worlds are assembled from the
    ///     transferred binaries and template worlds just mount the authored TerrainData, both as the same Terrain object
    /// </summary>
    public static class TerrainRuntimeBuilder
    {
        private const string TemplateTerrainDataAddress = "Vanilla/Environment/TemplateTerrainData";
        private const string TerrainObjectName = "Terrain";

        // URPのdefaultTerrainMaterialはエディタ専用でビルドではnullを返すため、プロジェクト所有のマテリアルをアドレスから引く
        // URP's defaultTerrainMaterial is editor-only and returns null in builds, so a project-owned material is resolved by address
        private const string TerrainMaterialAddress = "Vanilla/Environment/Terrain/TerrainLitMaterial";

        // Environment.prefabのTerrainが持っていたオーサリング配置の移設先。prefab側は削除済みで、
        // この定数がTemplateTerrainDataの配置を記録する唯一の場所になっている
        // sizeは2048角なのに位置は-1000で、中心合わせでは24mずれてベイク済みmapObject座標が全部崩れる
        // Migrated from the authored placement on Environment.prefab's Terrain, which has since been deleted,
        // leaving this constant as the only record of where TemplateTerrainData belongs
        // Its size is 2048 square yet the position is -1000, so centering it would shift 24m and break every baked mapObject coordinate
        private static readonly Vector3 TemplateTerrainOrigin = new(-1000f, 0f, -1000f);

        public static async UniTask BuildAsync(GetMapDataProtocol.ResponseMapDataMessagePack mapLayout, Transform environmentRoot)
        {
            // マテリアルはモードに依らず全タイル共通なので、分岐の前に1度だけ解決する
            // The material is shared by every tile regardless of mode, so resolve it once before branching
            var terrainMaterial = await AddressableLoader.LoadAsyncDefault<Material>(TerrainMaterialAddress);
            if (terrainMaterial == null)
                throw new InvalidOperationException(
                    $"[TerrainRuntimeBuilder] Terrain material '{TerrainMaterialAddress}' could not be loaded from Addressables.");

            if (mapLayout.MapMode == WorldProvisioner.TemplateMapMode)
                await BuildTemplateTerrainAsync(environmentRoot, terrainMaterial);
            else if (mapLayout.MapMode == WorldProvisioner.GeneratedMapMode)
                await BuildGeneratedTerrainAsync(mapLayout, environmentRoot, terrainMaterial);
            else
                // 未知のモードをgenerated扱いすると、地形の無いワールドでキャッシュ読み出しが不可解に落ちる
                // Treating an unknown mode as generated would fail obscurely in the cache read of a terrain-less world
                throw new InvalidOperationException($"[TerrainRuntimeBuilder] Unknown map mode '{mapLayout.MapMode}'.");

            // 露頭生成はこの直後に地表へレイキャストを飛ばす。新しいコライダーを物理シーンへ確実に反映させる
            // Outcrop instantiation raycasts the ground right after this, so the new colliders are pushed into the physics scene
            Physics.SyncTransforms();
        }

        // templateは地形バイナリを持たないワールド。見た目は従来どおりオーサリング済みTerrainDataのまま
        // A template world owns no terrain binary; its look stays exactly the authored TerrainData as before
        private static async UniTask BuildTemplateTerrainAsync(Transform environmentRoot, Material terrainMaterial)
        {
            var templateTerrainData = await AddressableLoader.LoadAsyncDefault<TerrainData>(TemplateTerrainDataAddress);
            if (templateTerrainData == null)
                throw new InvalidOperationException(
                    $"[TerrainRuntimeBuilder] Template TerrainData '{TemplateTerrainDataAddress}' could not be loaded from Addressables.");

            TerrainObjectFactory.Create(environmentRoot, TerrainObjectName, TemplateTerrainOrigin, templateTerrainData, terrainMaterial);
        }

        private static async UniTask BuildGeneratedTerrainAsync(
            GetMapDataProtocol.ResponseMapDataMessagePack mapLayout, Transform environmentRoot, Material terrainMaterial)
        {
            var buildStopwatch = Stopwatch.StartNew();
            var terrainSource = await GeneratedTerrainSource.CreateAsync(mapLayout);
            var terrainsByTileCoordinate = new Dictionary<Vector2Int, UnityEngine.Terrain>();
            var visualCacheHitCount = 0;

            // タイルの並びは転送ストリームの定義（正方格子・z行→x列）をそのまま使う
            // The tile order reuses the transfer stream's own definition: a square grid scanned row (z) then column (x)
            foreach (var tile in TerrainTransferMeta.EnumerateTileCoordinates(mapLayout.TerrainTileCount))
            {
                var terrainData = terrainSource.CreateTerrainData(tile.TileX, tile.TileZ, out var visualCacheHit);
                if (visualCacheHit) visualCacheHitCount++;

                var terrain = TerrainObjectFactory.Create(
                    environmentRoot, $"{TerrainObjectName}_{tile.TileX}_{tile.TileZ}",
                    terrainSource.TileWorldPosition(tile.TileX, tile.TileZ), terrainData, terrainMaterial);

                terrainsByTileCoordinate[new Vector2Int(tile.TileX, tile.TileZ)] = terrain;
            }

            TerrainNeighborLinker.Link(terrainsByTileCoordinate);

            // 見た目キャッシュの効きは1行で測る。初回と2回目の差はこのヒット数と所要時間に出る
            // One line measures how well the visual cache works; the first and second runs differ in this hit count and elapsed time
            Debug.Log($"[TerrainRuntimeBuilder] Generated terrain built: tiles={terrainsByTileCoordinate.Count} " +
                      $"visualCacheHits={visualCacheHitCount} elapsedMs={buildStopwatch.ElapsedMilliseconds}");
        }
    }
}
