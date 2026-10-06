using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Transfer;

namespace Game.MapGeneration.Pipeline.Surface
{
    public static class SurfaceGridBuilder
    {
        public static SurfaceTileGrid Build(TerrainGenerationConfig config)
        {
            SurfaceGenerationValidation.Validate(config, SurfaceEnvelope.GeneratedV5);
            var origins = MapGenerationPipeline.ResolveOrigins(config);
            var output = new MapGenerationOutput
            {
                Resolution = config.Resolution,
                NoiseOrigin = origins.NoiseOrigin,
                SceneOrigin = origins.SceneOrigin,
            };

            // 全タイル分類を確定してから陸地支持領域を構築する
            // Settle every tile classification before constructing land support
            var biomes = ClassificationStage.GetEnabledBiomeTypes(config);
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = origins.NoiseOrigin.x;
            gridConfig.worldOffsetZ = origins.NoiseOrigin.y;
            var masks = new bool[config.gridSizeX * config.gridSizeZ][];
            using var parameters = new SurfaceGenerationParameters(config, biomes);
            foreach (var (x, z) in TerrainTransferMeta.EnumerateTileCoordinates(masks.Length))
            {
                var tileConfig = gridConfig.CreateTileConfig(x, z);
                using var window = new SurfaceGenerationWindow(tileConfig, biomes, parameters);
                window.Run(tileConfig, biomes);
                var mask = new bool[config.Resolution * config.Resolution];
                for (int i = 0; i < mask.Length; i++)
                {
                    float value = window.Buffers.landMask[i];
                    if (!SurfaceGenerationValidation.Finite(value))
                        throw SurfaceGenerationValidation.Failure(config, $"{x},{z}", $"Non-finite land mask at {i}.");
                    mask[i] = value > 0.5f;
                }

                // 高さと分類だけを残し、大きい分類バッファは直ちに解放する
                // Retain only heights and classification, releasing large job buffers immediately
                masks[output.Tiles.Count] = mask;
                output.Tiles.Add(new TerrainTileOutput { TileX = x, TileZ = z, Heights = window.Buffers.heights.ToArray() });
            }
            return new SurfaceTileGrid(output, masks, config);
        }
    }
}
