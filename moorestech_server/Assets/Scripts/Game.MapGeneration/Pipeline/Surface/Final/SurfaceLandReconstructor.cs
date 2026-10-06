using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class SurfaceLandReconstructor
    {
        private readonly TerrainGenerationConfig _config;
        private LandCellField _resolved;

        internal SurfaceLandReconstructor(TerrainGenerationConfig config)
        {
            _config = config;
        }

        internal LandCellField Resolve()
        {
            if (_resolved != null) return _resolved;
            int stride = _config.Resolution - 1;
            var geometry = SurfaceLattice.ForWorld(_config, _config.TileScenePosition(0, 0));
            int width = geometry.Width;
            int depth = geometry.Depth;
            var mask = new bool[width * depth];
            var biomes = ClassificationStage.GetEnabledBiomeTypes(_config);
            using var parameters = new SurfaceGenerationParameters(_config, biomes);
            var boundaries = new SurfaceBoundarySamples(_config, biomes.Length);

            // 隣接タイルの角も保持し陸支持を復元
            // Keep neighbor tile corners to restore land support
            for (int tileZ = 0; tileZ < _config.gridSizeZ; tileZ++)
            for (int tileX = 0; tileX < _config.gridSizeX; tileX++)
            {
                var tile = _config.CreateTileConfig(tileX, tileZ);
                using var window = new SurfaceGenerationWindow(tile, biomes, parameters);
                window.Run(tile, biomes, boundaries, tileX, tileZ);
                for (int z = 0; z < tile.Resolution; z++)
                for (int x = 0; x < tile.Resolution; x++)
                {
                    float value = window.Buffers.landMask[z * tile.Resolution + x];
                    if (!SurfaceGenerationValidation.Finite(value))
                        throw SurfaceGenerationValidation.Failure(_config, $"{tileX},{tileZ}", "Non-finite reconstructed land mask.");
                    mask[(tileZ * stride + z) * width + tileX * stride + x] = 0.5f < value;
                }
            }
            _resolved = new LandCellField(geometry, mask);
            return _resolved;
        }
    }
}
