using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using Server.Protocol.PacketResponse;
using UnityEngine;

// System.Diagnostics を丸ごと開くと Debug が UnityEngine.Debug と衝突する
// Opening all of System.Diagnostics would collide Debug with UnityEngine.Debug
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Client.Game.InGame.Environment.Terrain
{
    /// <summary>
    ///     ワールドの地形をシーンへ建てる唯一の入口。生成システムのファサード(WorldTerrainSession)が返す結果を建てる
    ///     結果種別によらず同じTerrain GameObjectを生成
    ///     The single entry point standing a world's terrain up in the scene from what the generation system's facade (WorldTerrainSession) returns
    ///     Produces the same Terrain GameObject regardless of the result kind
    /// </summary>
    public static class TerrainRuntimeBuilder
    {
        private const string TerrainObjectName = "Terrain";

        // URPのdefaultTerrainMaterialはエディタ専用でビルドではnullを返すため、プロジェクト所有のマテリアルをアドレスから引く
        // URP's defaultTerrainMaterial is editor-only and returns null in builds, so a project-owned material is resolved by address
        private const string TerrainMaterialAddress = "Vanilla/Environment/Terrain/TerrainLitMaterial";

        public static async UniTask BuildAsync(GetMapDataProtocol.ResponseMapDataMessagePack mapLayout, Transform environmentRoot, string localMasterDirectory)
        {
            var terrainMaterial = await AddressableLoader.LoadAsyncDefault<Material>(TerrainMaterialAddress);
            if (terrainMaterial == null)
                throw new InvalidOperationException($"[TerrainRuntimeBuilder] Terrain material '{TerrainMaterialAddress}' could not be loaded from Addressables.");

            // 生成システムへはメタをそのまま戻す。中身（seed・原点）はここでは解釈しない
            // The meta goes straight back to the generation system; nothing here interprets its contents (seed, origins)
            var session = WorldTerrainSession.Open(mapLayout.TerrainMeta.ToTerrainTransferMeta(), localMasterDirectory);
            var layout = session.Layout;
            switch (layout.Kind)
            {
                // TileMapsとTiledTerrainSessionはOpenが対で決める。焼く口はこの分岐でしか要らない
                // Open settles TileMaps and TiledTerrainSession as a pair; baking is needed in this branch alone
                case TerrainLayoutKind.TerrainAsset: await BuildTerrainAssetAsync(); break;
                case TerrainLayoutKind.TileMaps: await BuildTileMapsAsync((TiledTerrainSession)session); break;
                default: throw new InvalidOperationException($"[TerrainRuntimeBuilder] Unknown layout kind {layout.Kind}.");
            }

            #region Internal

            async UniTask BuildTerrainAssetAsync()
            {
                var terrainData = await AddressableLoader.LoadAsyncDefault<TerrainData>(layout.AuthoredTerrainDataAddress);
                if (terrainData == null)
                    throw new InvalidOperationException(
                        $"[TerrainRuntimeBuilder] TerrainData '{layout.AuthoredTerrainDataAddress}' could not be loaded from Addressables.");
                TerrainObjectFactory.Create(environmentRoot, TerrainObjectName, layout.AuthoredOrigin, terrainData, terrainMaterial,
                    layout.DetailObjectDistance, layout.DetailObjectDensity);
            }

            async UniTask BuildTileMapsAsync(TiledTerrainSession tiledSession)
            {
                var buildStopwatch = Stopwatch.StartNew();
                var terrainLayers = await TerrainLayerAssetLoader.LoadAsync(layout.TextureLayerAddresses);
                var detailPrototypes = await DetailPrototypeAssetResolver.ResolveAsync(layout.DetailPrototypes);
                var terrainsByTileCoordinate = new Dictionary<Vector2Int, UnityEngine.Terrain>();
                foreach (var (tileX, tileZ) in layout.TileCoordinates)
                {
                    var tile = tiledSession.BakeTile(tileX, tileZ);
                    var terrainData = await TerrainDataAssembler.AssembleAsync(layout, tile, detailPrototypes, terrainLayers);
                    var terrain = TerrainObjectFactory.Create(environmentRoot, $"{TerrainObjectName}_{tileX}_{tileZ}", tile.ScenePosition,
                        terrainData, terrainMaterial, layout.DetailObjectDistance, layout.DetailObjectDensity);
                    terrainsByTileCoordinate[new Vector2Int(tileX, tileZ)] = terrain;
                }
                TerrainNeighborLinker.Link(terrainsByTileCoordinate);
                Debug.Log($"[TerrainRuntimeBuilder] Terrain built: tiles={terrainsByTileCoordinate.Count} elapsedMs={buildStopwatch.ElapsedMilliseconds}");
            }

            #endregion
        }
    }
}
