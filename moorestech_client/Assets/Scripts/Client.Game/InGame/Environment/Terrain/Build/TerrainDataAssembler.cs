using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Visual.Cache;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     出来上がった高さ・splatmap・detail密度をTerrainDataへ載せるだけの最終段。どの高さを渡すかは
    ///     呼び出し側の判断で、ここは受け取ったものをUnityの設定順どおりに流し込む
    ///     The final stage mounting finished heights, splatmap and detail densities onto a TerrainData; which heights
    ///     arrive is the caller's decision and this only feeds them in Unity's required order
    /// </summary>
    public static class TerrainDataAssembler
    {
        // 移植元と同じパッチ解像度。SetDetailResolutionの第2引数で描画パッチの粒度を決める
        // The source's patch resolution; SetDetailResolution's second argument sets the render patch granularity
        private const int DetailResolutionPerPatch = 16;

        public static async UniTask<TerrainData> AssembleAsync(
            TerrainGenerationConfig config, float[,] displayHeights, TerrainTileVisual tileVisual,
            List<DetailPrototype> detailPrototypes, TerrainLayer[] terrainLayers)
        {
            var terrainData = new TerrainData();
            ApplyHeightmap();
            await ApplySplatmapAsync();
            ApplyDetail();
            return terrainData;

            #region Internal

            // heightmapResolutionを先に入れる。後から変えるとsizeもSetHeightsの結果も作り直される
            // heightmapResolution comes first: changing it afterwards rebuilds both size and the SetHeights result
            void ApplyHeightmap()
            {
                terrainData.heightmapResolution = config.Resolution;
                terrainData.size = new Vector3(config.terrainWidth, config.terrainHeight, config.terrainLength);
                terrainData.SetHeights(0, 0, displayHeights);
            }

            async UniTask ApplySplatmapAsync()
            {
                terrainData.alphamapResolution = tileVisual.Alphamap.GetLength(0);
                terrainData.terrainLayers = terrainLayers;
                await TerrainAlphamapApplier.ApplyAsync(terrainData, tileVisual.Alphamap);
            }

            void ApplyDetail()
            {
                if (detailPrototypes.Count == 0) return;

                var detailMaps = tileVisual.DetailMaps;
                terrainData.SetDetailResolution(detailMaps[0].GetLength(0), DetailResolutionPerPatch);

                // CoverageModeではメッシュDetailが描画されないことがあるため移植元と同じくInstanceCountModeにする
                // CoverageMode can leave mesh details undrawn, so InstanceCountMode is used as in the source
                terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                terrainData.detailPrototypes = detailPrototypes.ToArray();

                for (var layerIndex = 0; layerIndex < detailMaps.Count; layerIndex++)
                    terrainData.SetDetailLayer(0, 0, layerIndex, detailMaps[layerIndex]);
            }

            #endregion
        }
    }
}
