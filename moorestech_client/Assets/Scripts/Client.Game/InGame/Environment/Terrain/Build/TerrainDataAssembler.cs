using System.Collections.Generic;
using System.Linq;
using Core.Master.Validator;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     出来上がった高さ・splatmap・detail密度をTerrainDataへ載せる最終段。Unityの設定順どおりに流し込む
    ///     適用可否はBakedTerrainTileの中身が決める
    ///     The final stage mounting finished heights, splatmap and detail densities onto a TerrainData in Unity's required order
    ///     What applies is decided by the baked tile's own contents
    /// </summary>
    public static class TerrainDataAssembler
    {
        public static async UniTask<TerrainData> AssembleAsync(
            WorldTerrainLayout layout, BakedTerrainTile tile,
            IReadOnlyList<DetailPrototype> detailPrototypes, TerrainLayer[] terrainLayers)
        {
            var terrainData = new TerrainData();
            ApplyHeightmap();
            await TerrainAlphamapApplier.ApplyAsync(terrainData, terrainLayers, tile);
            // ApplyDetail();
            return terrainData;

            #region Internal

            // heightmapResolutionを先に入れる。後から変えるとsizeもSetHeightsの結果も作り直される
            // heightmapResolution comes first: changing it afterwards rebuilds both size and the SetHeights result
            void ApplyHeightmap()
            {
                terrainData.heightmapResolution = layout.HeightmapResolution;
                terrainData.size = layout.TileSize;
                terrainData.SetHeights(0, 0, tile.DisplayHeights);
            }



            // native TerrainDataを作る前に、全detail入力の本数と寸法を確定する
            // Settle every detail count and dimension before allocating the native TerrainData
            int ValidateDetailInputs()
            {
                var detailMaps = tile.DetailMaps;
                if (detailMaps.Count == 0) return 0;

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

                return resolution;
            }

            #endregion
        }
    }
}
