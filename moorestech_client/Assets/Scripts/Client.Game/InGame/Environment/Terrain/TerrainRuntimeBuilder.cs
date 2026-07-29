using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Server.Protocol.PacketResponse;
using UnityEngine;

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

        // templateワールドの地形配置はEnvironment.prefabが持っていたオーサリング値。sizeが2048角なのに対し
        // 位置は-1000なので、ベイク済みmapObject座標と揃えるにはこの実測値そのものが要る
        // The template world's terrain placement is the authored value Environment.prefab carried; its size is 2048
        // square while the position is -1000, so matching the baked mapObject coordinates needs this exact value
        private static readonly Vector3 TemplateTerrainOrigin = new(-1000f, 0f, -1000f);

        public static async UniTask BuildAsync(GetMapDataProtocol.ResponseMapDataMessagePack mapLayout, Transform environmentRoot)
        {
            if (mapLayout.MapMode == WorldProvisioner.TemplateMapMode)
                await BuildTemplateTerrainAsync(environmentRoot);
            else if (mapLayout.MapMode == WorldProvisioner.GeneratedMapMode)
                await BuildGeneratedTerrainAsync(mapLayout, environmentRoot);
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
        private static async UniTask BuildTemplateTerrainAsync(Transform environmentRoot)
        {
            var templateTerrainData = await AddressableLoader.LoadAsyncDefault<TerrainData>(TemplateTerrainDataAddress);
            if (templateTerrainData == null)
                throw new InvalidOperationException(
                    $"[TerrainRuntimeBuilder] Template TerrainData '{TemplateTerrainDataAddress}' could not be loaded from Addressables.");

            TerrainObjectFactory.Create(environmentRoot, TerrainObjectName, TemplateTerrainOrigin, templateTerrainData);
        }

        private static async UniTask BuildGeneratedTerrainAsync(
            GetMapDataProtocol.ResponseMapDataMessagePack mapLayout, Transform environmentRoot)
        {
            var terrainSource = await GeneratedTerrainSource.CreateAsync(mapLayout);
            var terrainsByTileCoordinate = new Dictionary<Vector2Int, UnityEngine.Terrain>();

            // タイルの並びは転送ストリームの定義（正方格子・z行→x列）をそのまま使う
            // The tile order reuses the transfer stream's own definition: a square grid scanned row (z) then column (x)
            foreach (var tile in TerrainTransferMeta.EnumerateTileCoordinates(mapLayout.TerrainTileCount))
            {
                var terrainData = terrainSource.CreateTerrainData(tile.TileX, tile.TileZ);
                var terrain = TerrainObjectFactory.Create(
                    environmentRoot, $"{TerrainObjectName}_{tile.TileX}_{tile.TileZ}",
                    terrainSource.TileWorldPosition(tile.TileX, tile.TileZ), terrainData);

                terrainsByTileCoordinate[new Vector2Int(tile.TileX, tile.TileZ)] = terrain;
            }

            TerrainNeighborLinker.Link(terrainsByTileCoordinate);
        }
    }
}
