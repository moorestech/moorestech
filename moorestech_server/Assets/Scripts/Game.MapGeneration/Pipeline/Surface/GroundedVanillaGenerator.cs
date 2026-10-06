using Game.MapGeneration.Pipeline.Surface.Placement;
using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Surface.Origins;
using Game.MapGeneration.Pipeline.Tiling;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class GroundedVanillaGenerator : IMapGenerator
    {
        private readonly SurfaceEnvelope _envelope;

        public GroundedVanillaGenerator(SurfaceEnvelope envelope)
        {
            _envelope = envelope;
        }

        public GenerationRun Generate(TerrainGenerationConfig sourceConfig)
        {
            var config = sourceConfig.ShallowCopy();
            var envelope = _envelope;

            // 自分の版を刻み、版違いの config で呼ばれても表示と接地が食い違わないようにする
            // Stamp our own revision so a config from another revision cannot split display and grounding
            config.surfaceRevision = WorldSurfaceRevision.Grounded5;
            SurfaceGenerationValidation.Validate(config, envelope);
            var biomes = ClassificationStage.GetEnabledBiomeTypes(config);
            GenerationOriginResolver.RunSpawnSearch(config, biomes);
            var shift = new Vector2(config.worldOffsetX, config.worldOffsetZ);
            var spawn = GenerationOriginResolver.ComputeSceneSpawnXz(config, shift);

            // 配置の前に全域の陸地と海面下限を確定する
            // Settle world-wide land classification and the sea floor before placement
            var grid = SurfaceGridBuilder.Build(config);
            grid.ApplyLandFloor(envelope);
            var ledger = new PlacementLedger();
            var bindings = new SurfacePlacementBindings(config);
            var helper = new BiomePlacementHelper(config);
            var halo = new PlacementHaloStore(PlacementHaloRadius.Resolve(config, biomes, helper));
            var placementRule = new GroundedVeinPlacementRule(grid.Land, shift, envelope, config.surfaceRevision);
            var runner = new TilePlacementRunner(helper, biomes, shift,
                new Vector3(spawn.x, 0f, spawn.y), grid.Output, halo, ledger, placementRule, bindings);
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = grid.Output.NoiseOrigin.x;
            gridConfig.worldOffsetZ = grid.Output.NoiseOrigin.y;

            // 配置用分類を再生成、高さは確定済み。全タイルの分類バッファを保持すると生物群系数×解像度²×タイル数を常駐させるため、陸確定後にもう1周回す
            // Regenerate placement classification; heights are settled. Holding every tile's classification buffers would keep biomes x resolution^2 x tiles resident, so a second pass runs once land settles
            using var parameters = new SurfaceGenerationParameters(config, biomes);
            var boundaries = new SurfaceBoundarySamples(config, biomes.Length);
            foreach (var tile in grid.Output.Tiles)
            {
                var tileConfig = gridConfig.CreateTileConfig(tile.TileX, tile.TileZ);
                using var window = new SurfaceGenerationWindow(tileConfig, biomes, parameters);
                window.Run(tileConfig, biomes, boundaries, tile.TileX, tile.TileZ);
                runner.Run(tileConfig, window.Buffers, tile.Heights, config.TileScenePosition(tile.TileX, tile.TileZ), tile.TileX, tile.TileZ);
            }

            // 整地前後を表示と同じpolicyで評価し、配置物とスポーンを整地後の表示地表へ載せ直す
            // Evaluate before and after grading with the display policies and re-anchor objects and spawn onto the graded display surface
            var before = SurfaceDisplayEvaluator.Build(grid, ledger, new PreGradingSurfaceHeightPolicy());
            ledger = VeinGroundingPlanner.Build(grid, envelope).Apply(ledger);
            var displayPolicy = new GroundedSurfaceHeightPolicy(grid.Land, envelope);
            var after = SurfaceDisplayEvaluator.Build(grid, ledger, displayPolicy);
            ledger = SurfaceObjectReanchor.Apply(grid.Output, ledger, bindings, before, after);
            grid.Output.SpawnPoint = new Vector3(spawn.x, after.SampleHeight(spawn), spawn.y);
            return new GenerationRun(grid.Output, ledger, config, displayPolicy);
        }
    }
}
