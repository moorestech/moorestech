using System.Collections.Generic;
using System.Linq;
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
                // 非生成時はUnity既定のalphamapを維持
                // When not generating, Unity's default alphamap is kept
                if (tile.Alphamap == null) return;

                terrainData.alphamapResolution = tile.Alphamap.GetLength(0);
                terrainData.terrainLayers = terrainLayers;
                await TerrainAlphamapApplier.ApplyAsync(terrainData, tile.Alphamap);
            }

            void ApplyDetail()
            {
                var detailMaps = tile.DetailMaps;
                if (detailMaps.Count == 0) return;

                // プロトタイプ数と密度マップ本数は生成側の1:1対応が保証しているだけで、ここは知らない前提で組む
                // The prototype count and density-map count agree only because the generator guarantees it 1:1; this stage assumes nothing on its own
                if (detailPrototypes.Count != detailMaps.Count)
                    throw new System.InvalidOperationException(
                        $"[TerrainDataAssembler] Detail prototype count {detailPrototypes.Count} does not match detail map count {detailMaps.Count}.");

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
