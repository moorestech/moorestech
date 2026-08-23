using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     splatmapの列順どおりにTerrainLayerを解決する。SplatLayerTableが決めた並びがそのままTerrainDataの
    ///     terrainLayersになるため、1本でも取り違えると全画素のテクスチャが入れ替わる
    ///     Resolves TerrainLayers in the splatmap's column order; that order becomes TerrainData.terrainLayers
    ///     verbatim, so a single mismatch swaps the texture of every pixel
    /// </summary>
    public static class TerrainLayerAssetLoader
    {
        public static async UniTask<TerrainLayer[]> LoadAsync(IReadOnlyList<string> orderedLayerAddresses)
        {
            var terrainLayers = new TerrainLayer[orderedLayerAddresses.Count];

            for (var index = 0; index < orderedLayerAddresses.Count; index++)
            {
                var layerAddress = orderedLayerAddresses[index];
                var terrainLayer = await AddressableLoader.LoadAsyncDefault<TerrainLayer>(layerAddress);

                // 解決できない1本を飛ばすと以降の列が繰り上がる。欠番のまま進めず落とす
                // Skipping one unresolved layer would shift every later column up, so this fails instead of leaving a gap
                if (terrainLayer == null)
                    throw new InvalidOperationException(
                        $"[TerrainLayerAssetLoader] TerrainLayer '{layerAddress}' could not be loaded from Addressables.");

                terrainLayers[index] = terrainLayer;
            }

            return terrainLayers;
        }
    }
}
