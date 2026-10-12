using System;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal static class SurfaceGenerationValidation
    {
        internal static void Validate(TerrainGenerationConfig config, SurfaceEnvelope envelope)
        {
            // 寸法と分類値を入口で検証
            // Validate dimensions and classification values at the entry
            if (!Finite(config.terrainHeight) || config.terrainHeight <= 0f ||
                !Finite(config.terrainWidth) || config.terrainWidth <= 0f ||
                !Finite(config.terrainLength) || config.terrainLength <= 0f || config.Resolution < 2 ||
                config.gridSizeX <= 0 || config.gridSizeX != config.gridSizeZ)
                throw Failure(config, "all", "Invalid terrain dimensions or grid.");
            if (!Finite(config.seaLevel) || config.seaLevel < 0f || 1f < config.seaLevel)
                throw Failure(config, "all", "Classification seaLevel must be finite in [0,1].");
            if (!config.generateHeightmap)
                throw Failure(config, "all", "Heightmap-disabled previews cannot generate a guaranteed world.");

            // 包絡と採掘底面の存在を独立検証
            // Validate the envelope and mining bottom independently
            if (!Finite(envelope.SeaY) || !Finite(envelope.MaximumWaveRise) || envelope.MaximumWaveRise < 0f ||
                !Finite(envelope.LandClearance) || envelope.LandClearance < 0f ||
                !Finite(envelope.CoreHalfSize) || envelope.CoreHalfSize <= 0f ||
                !Finite(envelope.BlendWidth) || envelope.BlendWidth <= 0f)
                throw Failure(config, "all", "Invalid surface envelope.");
            var minimumBottom = SurfaceQuantization.MinimumMiningBottom(config, envelope, "all");
            if (minimumBottom < 0d || Math.Floor(config.terrainHeight) < minimumBottom)
                throw Failure(config, "all", "No integer mining bottom fits above the land floor.");
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static InvalidOperationException Failure(TerrainGenerationConfig config, string tile, string reason)
        {
            var message = $"[GeneratedSurface] seed={config.seed} revision={config.surfaceRevision} tile={tile}: {reason}";
            Debug.LogError(message);
            return new InvalidOperationException(message);
        }
    }
}
