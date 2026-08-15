using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Environment.Terrain.Visual.Cache;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     出来上がった高さ・splatmap・detail密度をTerrainDataへ載せる最終段。Unityの設定順どおりに流し込みつつ、
    ///     どれを実際に適用するかはconfigのgenerate系フラグが決める（移植元TerrainApplierが結果の欠落で止めていたのと同じ分岐）
    ///     The final stage mounting finished heights, splatmap and detail densities onto a TerrainData in Unity's required
    ///     order, with the config's generate flags deciding which apply at all, as the source TerrainApplier did via absent results
    /// </summary>
    public static class TerrainDataAssembler
    {
        // 移植元と同じパッチ解像度。SetDetailResolutionの第2引数で描画パッチの粒度を決める
        // The source's patch resolution; SetDetailResolution's second argument sets the render patch granularity
        private const int DetailResolutionPerPatch = 16;

        public static async UniTask<TerrainData> AssembleAsync(
            TerrainGenerationConfig config, float[,] displayHeights, TerrainTileVisual tileVisual,
            IReadOnlyList<DetailPrototype> detailPrototypes, TerrainLayer[] terrainLayers)
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

                // 切れるのは高さの適用だけ。解像度とsizeはTerrainの器そのもので、落とすとタイルが縮む（移植元TerrainGenerator.cs:212）
                // Only applying the heights is gated; resolution and size are the terrain's own shape and dropping them shrinks the tile (source TerrainGenerator.cs:212)
                if (!config.generateHeightmap) return;

                terrainData.SetHeights(0, 0, displayHeights);
            }

            async UniTask ApplySplatmapAsync()
            {
                // テクスチャを作らない設定ではalphamapが存在しない。Unity既定のalphamapのままにする（移植元TerrainGenerator.cs:216）
                // A config building no texture owns no alphamap, so Unity's default one is left in place (source TerrainGenerator.cs:216)
                if (!config.generateTexture) return;

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
