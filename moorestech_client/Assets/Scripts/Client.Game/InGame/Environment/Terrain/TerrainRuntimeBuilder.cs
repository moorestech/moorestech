using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
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
        private const float TemplateDetailObjectDistance = 80f;
        private const float TemplateDetailObjectDensity = 1f;
        private const float GeneratedDetailObjectDistance = 200f;
        private const float GeneratedDetailObjectDensity = 0.3f;

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

            // モード解釈はToTerrainTransferMeta1本。未知モードもそこで例外になる
            // ToTerrainTransferMeta is the only mode interpreter, and it is also where unknown modes throw
            var wireMeta = mapLayout.TerrainMeta;
            var terrainMeta = wireMeta.ToTerrainTransferMeta();
            if (terrainMeta.IsTemplate)
                await BuildTemplateTerrainAsync();
            else
                await BuildGeneratedTerrainAsync();

            #region Internal

            // templateは地形バイナリを持たないワールド。見た目は従来どおりオーサリング済みTerrainDataのまま
            // A template world owns no terrain binary; its look stays exactly the authored TerrainData as before
            async UniTask BuildTemplateTerrainAsync()
            {
                var templateTerrainData = await AddressableLoader.LoadAsyncDefault<TerrainData>(TemplateTerrainDataAddress);
                if (templateTerrainData == null)
                    throw new InvalidOperationException(
                        $"[TerrainRuntimeBuilder] Template TerrainData '{TemplateTerrainDataAddress}' could not be loaded from Addressables.");

                TerrainObjectFactory.Create(
                    environmentRoot, TerrainObjectName, TemplateTerrainOrigin, templateTerrainData, terrainMaterial,
                    TemplateDetailObjectDistance, TemplateDetailObjectDensity);
            }

            // mapObjectsはシーン絶対座標の全タイルぶん。木の高さ摂動が転送高さの意味(R12)を表示用へ戻すのに要る
            // The map objects arrive scene-absolute for every tile; the tree height perturbation needs them to turn the transferred meaning (R12) back into display heights
            async UniTask BuildGeneratedTerrainAsync()
            {
                var buildStopwatch = Stopwatch.StartNew();
                var terrainSource = await GeneratedTerrainSource.CreateAsync(terrainMeta, wireMeta.TerrainHash, mapLayout.MapObjects);
                var terrainsByTileCoordinate = new Dictionary<Vector2Int, UnityEngine.Terrain>();

                // タイルの並びは転送ストリームの定義（正方格子・z行→x列）をそのまま使う
                // The tile order reuses the transfer stream's own definition: a square grid scanned row (z) then column (x)
                foreach (var tile in TerrainTransferMeta.EnumerateTileCoordinates(terrainMeta.TerrainTileCount))
                {
                    var terrainData = await terrainSource.CreateTerrainDataAsync(tile.TileX, tile.TileZ);

                    var terrain = TerrainObjectFactory.Create(
                        environmentRoot, $"{TerrainObjectName}_{tile.TileX}_{tile.TileZ}",
                        terrainSource.TileWorldPosition(tile.TileX, tile.TileZ), terrainData, terrainMaterial,
                        GeneratedDetailObjectDistance, GeneratedDetailObjectDensity);

                    terrainsByTileCoordinate[new Vector2Int(tile.TileX, tile.TileZ)] = terrain;
                }

                TerrainNeighborLinker.Link(terrainsByTileCoordinate);

                Debug.Log($"[TerrainRuntimeBuilder] Generated terrain built: tiles={terrainsByTileCoordinate.Count} " +
                          $"elapsedMs={buildStopwatch.ElapsedMilliseconds}");
            }

            #endregion
        }
    }
}
