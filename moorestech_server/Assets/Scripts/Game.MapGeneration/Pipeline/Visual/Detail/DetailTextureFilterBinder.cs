using System;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Detail
{
    internal static class DetailTextureFilterBinder
    {
        internal static void Apply(BiomeVisualSections visualSections, SplatLayerTable layerTable)
        {
            // レイヤー表確定後に列番号を渡す
            // Pass column indices after the layer table settles
            foreach (var detailConfig in visualSections.DetailConfigs)
            foreach (var entry in detailConfig.entries)
            {
                var textureFilter = entry.textureFilter;
                if (!textureFilter.enabled || textureFilter.entries == null) continue;

                foreach (var filterEntry in textureFilter.entries)
                {
                    if (!layerTable.LayerIndexByAddress.TryGetValue(filterEntry.layerAddressablePath, out var layerIndex))
                    {
                        string reason = $"[TileVisualBaker] Detail texture filter layer '{filterEntry.layerAddressablePath}' is not registered in the splatmap layer table.";
                        Debug.LogError(reason);
                        throw new InvalidOperationException(reason);
                    }

                    filterEntry.SetLayerIndex(layerIndex);
                }
            }
        }
    }
}
