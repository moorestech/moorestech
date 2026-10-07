using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Transfer;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal static class SurfaceGridBuilder
    {
        internal static SurfaceTileGrid Build(TerrainGenerationConfig config)
        {
            var origins = MapGenerationPipeline.ResolveOrigins(config);
            var output = new MapGenerationOutput
            {
                Resolution = config.Resolution,
                NoiseOrigin = origins.NoiseOrigin,
                SceneOrigin = origins.SceneOrigin,
            };

            // 全分類確定後に陸地支持を構築
            // Build land support after all classifications settle
            var biomes = ClassificationStage.GetEnabledBiomeTypes(config);
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = origins.NoiseOrigin.x;
            gridConfig.worldOffsetZ = origins.NoiseOrigin.y;
            var masks = new bool[config.gridSizeX * config.gridSizeZ][];
            using var parameters = new SurfaceGenerationParameters(config, biomes);
            var boundaries = new SurfaceBoundarySamples(config, biomes.Length);
            foreach (var (x, z) in TerrainTransferMeta.EnumerateTileCoordinates(masks.Length))
            {
                var tileConfig = gridConfig.CreateTileConfig(x, z);
                using var window = new SurfaceGenerationWindow(tileConfig, biomes, parameters);
                window.Run(tileConfig, biomes, boundaries, x, z);
                var mask = new bool[config.Resolution * config.Resolution];
                for (int i = 0; i < mask.Length; i++)
                {
                    float value = window.Buffers.landMask[i];
                    if (!SurfaceGenerationValidation.Finite(value))
                        throw SurfaceGenerationValidation.Failure(config, $"{x},{z}", $"Non-finite land mask at {i}.");
                    mask[i] = 0.5f < value;
                }

                // 高さと分類のみ残し分類バッファ解放
                // Keep heights and classification, release the buffers
                masks[output.Tiles.Count] = mask;
                output.Tiles.Add(new TerrainTileOutput { TileX = x, TileZ = z, Heights = window.Buffers.heights.ToArray() });
            }
            return new SurfaceTileGrid(output, masks, config);
        }
    }
}
