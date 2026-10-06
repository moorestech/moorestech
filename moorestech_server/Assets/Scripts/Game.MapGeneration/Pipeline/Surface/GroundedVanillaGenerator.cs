using Game.MapGeneration.Pipeline.Surface.Placement;
using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Surface.Origins;
using Game.MapGeneration.Pipeline.Tiling;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public sealed class GroundedVanillaGenerator : IMapGenerator
    {
        public GenerationRun Generate(TerrainGenerationConfig sourceConfig)
        {
            var config = sourceConfig.ShallowCopy();
            var envelope = SurfaceEnvelope.GeneratedV5;
            SurfaceGenerationValidation.Validate(config, envelope);
            var biomes = ClassificationStage.GetEnabledBiomeTypes(config);
            GenerationOriginResolver.RunSpawnSearch(config, biomes);
            var shift = new Vector2(config.worldOffsetX, config.worldOffsetZ);
            var spawn = GenerationOriginResolver.ComputeSceneSpawnXz(config, shift);

            // 配置の前に全域の陸地と海面下限を確定する
            // Settle world-wide land classification and the sea floor before placement
            var grid = SurfaceGridBuilder.BuildValidated(config);
            grid.ApplyLandFloor(envelope);
            var ledger = new PlacementLedger();
            var bindings = new SurfacePlacementBindings();
            var helper = new BiomePlacementHelper(config);
            var halo = new PlacementHaloStore(PlacementHaloRadius.Resolve(config, biomes, helper));
            var runner = new TilePlacementRunner(helper, biomes, shift,
                new Vector3(spawn.x, 0f, spawn.y), grid.Output, halo, ledger, new GroundedVeinLandConstraint(grid.Land, shift, envelope), bindings);
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = grid.Output.NoiseOrigin.x;
            gridConfig.worldOffsetZ = grid.Output.NoiseOrigin.y;

            // 配置用分類を再生成し、高さだけ確定済み配列を使う
            // Regenerate placement classification while using the settled height arrays
            using var parameters = new SurfaceGenerationParameters(config, biomes);
            var boundaries = new SurfaceBoundarySamples(config, biomes.Length);
            foreach (var tile in grid.Output.Tiles)
            {
                var tileConfig = gridConfig.CreateTileConfig(tile.TileX, tile.TileZ);
                using var window = new SurfaceGenerationWindow(tileConfig, biomes, parameters);
                window.Run(tileConfig, biomes, boundaries, tile.TileX, tile.TileZ);
                runner.Run(tileConfig, window.Buffers, tile.Heights, config.TileScenePosition(tile.TileX, tile.TileZ), tile.TileX, tile.TileZ);
            }

            var before = SurfaceDisplayEvaluator.Build(grid, ledger, false, envelope);
            ledger = VeinGroundingPlanner.Build(grid, envelope).Apply(grid.Output, ledger);
            var after = SurfaceDisplayEvaluator.Build(grid, ledger, true, envelope);
            ledger = SurfaceObjectReanchor.Apply(grid.Output, ledger, bindings, before, after);
            grid.Output.SpawnPoint = new Vector3(spawn.x, after.SampleHeight(spawn), spawn.y);
            return new GenerationRun(grid.Output, ledger, config);
        }
    }
}
