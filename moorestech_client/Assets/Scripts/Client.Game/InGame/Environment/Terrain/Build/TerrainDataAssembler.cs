using System.Collections.Generic;
using System.Threading;
using Core.Master.Validator;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     出来上がった高さとsplatmapをTerrainDataへ載せる最終段。超軽量設定ではdetail描画を省く
    ///     The final stage mounting finished heights and splatmap onto TerrainData; the ultra-light preset skips detail rendering
    /// </summary>
    public static class TerrainDataAssembler
    {
        public static async UniTask<TerrainData> AssembleAsync(
            WorldTerrainLayout layout, BakedTerrainTile tile,
            IReadOnlyList<DetailPrototype> detailPrototypes, TerrainLayer[] terrainLayers, CancellationToken cancellationToken)
        {
            var terrainData = new TerrainData();
            var assembled = false;
            try
            {
                await AssembleIntoAsync(terrainData, layout, tile, detailPrototypes, terrainLayers, cancellationToken);
                assembled = true;
                return terrainData;
            }
            finally
            {
                // 失敗時だけ自身の確保分を破棄し、成功時の所有権は呼び手へ渡す
                // Release this allocation on failure and transfer ownership to the caller on success
                if (!assembled)
                {
                    if (Application.isPlaying) Object.Destroy(terrainData);
                    else Object.DestroyImmediate(terrainData);
                }
            }
        }

        public static async UniTask AssembleIntoAsync(
            TerrainData terrainData, WorldTerrainLayout layout, BakedTerrainTile tile,
            IReadOnlyList<DetailPrototype> detailPrototypes, TerrainLayer[] terrainLayers, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 超軽量設定でもdetail入力は地形を変更する前に検証する
            // Validate detail input before modifying terrain, even in the ultra-light preset
            ValidateDetailInputs();
            ApplyHeightmap();
            await TerrainAlphamapApplier.ApplyAsync(terrainData, terrainLayers, tile, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            #region Internal

            // heightmapResolutionを先に入れる。後から変えるとsizeもSetHeightsの結果も作り直される
            // heightmapResolution comes first: changing it afterwards rebuilds both size and the SetHeights result
            void ApplyHeightmap()
            {
                terrainData.heightmapResolution = layout.HeightmapResolution;
                terrainData.size = layout.TileSize;
                terrainData.SetHeights(0, 0, tile.DisplayHeights);
            }
            // native TerrainDataを変更する前に、全detail入力の本数と寸法を確定する
            // Settle every detail count and dimension before modifying the native TerrainData
            void ValidateDetailInputs()
            {
                var detailMaps = tile.DetailMaps;
                if (detailMaps.Count == 0) return;

                // プロトタイプ数と密度マップ本数は生成側の1:1対応が保証しているだけで、ここは知らない前提で組む
                // The prototype count and density-map count agree only because the generator guarantees it 1:1; this stage assumes nothing on its own
                if (detailPrototypes.Count != detailMaps.Count)
                    throw new System.InvalidOperationException(
                        $"[TerrainDataAssembler] Detail prototype count {detailPrototypes.Count} does not match detail map count {detailMaps.Count}.");

                var resolution = detailMaps[0].GetLength(0);
                if (!GenerationMasterUtil.IsValidDetailResolution(resolution, layout.HeightmapResolution))
                    throw new System.InvalidOperationException(
                        $"[TerrainDataAssembler] Detail resolution {resolution} {GenerationMasterUtil.DescribeDetailResolutionRule(layout.HeightmapResolution)}.");
                for (var layerIndex = 0; layerIndex < detailMaps.Count; layerIndex++)
                    if (detailMaps[layerIndex].GetLength(0) != resolution || detailMaps[layerIndex].GetLength(1) != resolution)
                        throw new System.InvalidOperationException(
                            $"[TerrainDataAssembler] Detail map {layerIndex} must be square and match resolution {resolution}.");

            }

            #endregion
        }
    }
}
