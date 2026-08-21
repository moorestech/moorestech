using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     出来上がった高さ・splatmap・detail密度をTerrainDataへ載せる最終段。Unityの設定順どおりに流し込みつつ、
    ///     どれを実際に適用するかはBakedTerrainTileの中身が決める（alphamapがnull・detailMapsが空ならその段は素通しする）
    ///     The final stage mounting finished heights, splatmap and detail densities onto a TerrainData in Unity's required
    ///     order, with the baked tile's own contents deciding what applies at all (a null alphamap or empty detail maps skip that stage)
    /// </summary>
    public static class TerrainDataAssembler
    {
        // 移植元と同じパッチ解像度。SetDetailResolutionの第2引数で描画パッチの粒度を決める
        // The source's patch resolution; SetDetailResolution's second argument sets the render patch granularity
        private const int DetailResolutionPerPatch = 16;

        public static async UniTask<TerrainData> AssembleAsync(
            WorldTerrainLayout layout, BakedTerrainTile tile,
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
                terrainData.heightmapResolution = layout.HeightmapResolution;
                terrainData.size = layout.TileSize;
                terrainData.SetHeights(0, 0, tile.DisplayHeights);
            }

            async UniTask ApplySplatmapAsync()
            {
                // テクスチャを作らない設定ではalphamapが存在しない。Unity既定のalphamapのままにする
                // A config building no texture owns no alphamap, so Unity's default one is left in place
                if (tile.Alphamap == null) return;

                terrainData.alphamapResolution = tile.Alphamap.GetLength(0);
                terrainData.terrainLayers = terrainLayers;
                await TerrainAlphamapApplier.ApplyAsync(terrainData, tile.Alphamap);
            }

            void ApplyDetail()
            {
                if (detailPrototypes.Count == 0) return;

                var detailMaps = tile.DetailMaps;
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
