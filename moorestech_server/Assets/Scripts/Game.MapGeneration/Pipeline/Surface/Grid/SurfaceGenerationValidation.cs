using System;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal static class SurfaceGenerationValidation
    {
        internal static void Validate(TerrainGenerationConfig config, SurfaceEnvelope envelope)
        {
            // 本番保存に必要な寸法と分類値を入口で検証する
            // Validate dimensions and classification inputs required for production saves
            if (!Finite(config.terrainHeight) || config.terrainHeight <= 0f ||
                !Finite(config.terrainWidth) || config.terrainWidth <= 0f ||
                !Finite(config.terrainLength) || config.terrainLength <= 0f || config.Resolution < 2 ||
                config.gridSizeX <= 0 || config.gridSizeX != config.gridSizeZ)
                throw Failure(config, "all", "Invalid terrain dimensions or grid.");
            if (!Finite(config.seaLevel) || config.seaLevel < 0f || config.seaLevel > 1f)
                throw Failure(config, "all", "Classification seaLevel must be finite in [0,1].");
            if (!config.generateHeightmap)
                throw Failure(config, "all", "Heightmap-disabled previews cannot generate a guaranteed world.");

            // 描画の包絡と採掘底面の存在を独立に検証する
            // Validate the rendering envelope and the existence of a mining bottom independently
            if (!Finite(envelope.SeaY) || !Finite(envelope.MaximumWaveRise) || envelope.MaximumWaveRise < 0f ||
                !Finite(envelope.LandClearance) || envelope.LandClearance < 0f ||
                !Finite(envelope.CoreHalfSize) || envelope.CoreHalfSize <= 0f ||
                !Finite(envelope.BlendWidth) || envelope.BlendWidth <= 0f)
                throw Failure(config, "all", "Invalid surface envelope.");
            var minimumBottom = Math.Ceiling(SurfaceQuantization.LandFloor(config, envelope, "all") +
                                            config.terrainHeight / (double)SurfaceQuantization.TerrainStorageSteps + 0.001d);
            if (minimumBottom < 0d || minimumBottom > Math.Floor(config.terrainHeight))
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
