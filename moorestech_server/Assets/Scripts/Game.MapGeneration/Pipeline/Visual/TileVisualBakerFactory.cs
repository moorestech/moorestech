using System;
using System.Collections.Generic;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Mooresmaster.Model.GenerationModule;

namespace Game.MapGeneration.Pipeline.Visual
{
    /// <summary>
    ///     ワールド同一性(転送メタ)から TileVisualBaker を組み立てる唯一の窓口。クライアントの WorldTerrainSession.Open と
    ///     サーバーの先焼き(TerrainVisualPrebake)が同じ組み立てを通るための切り出し
    ///     The single window building a TileVisualBaker from a world identity (transfer meta), so the client's
    ///     WorldTerrainSession.Open and the server's prebake (TerrainVisualPrebake) share the very same assembly
    /// </summary>
    public static class TileVisualBakerFactory
    {
        public readonly struct Result
        {
            public readonly TileVisualBaker Baker;
            public readonly TerrainGenerationConfig GridConfig;
            public readonly IReadOnlyList<string> OrderedLayerAddresses;

            // privateだと入れ子先のTileVisualBakerFactory自身からもコンストラクトできない(private accessibility domainはネスト型自身の中だけ)。internalで閉じる
            // private would block even the enclosing TileVisualBakerFactory from constructing it (a private member's accessibility domain is only the nested type itself); internal closes it correctly
            internal Result(TileVisualBaker baker, TerrainGenerationConfig gridConfig, IReadOnlyList<string> orderedLayerAddresses)
            {
                Baker = baker;
                GridConfig = gridConfig;
                OrderedLayerAddresses = orderedLayerAddresses;
            }
        }

        // heightSourceは呼び出し側指定（先焼き=terrain/、クライアント=共有キャッシュ）
        // heightSource is caller-specified (prebake = terrain/, client = shared cache)
        public static Result Create(
            TerrainGenerationConfig config, TerrainTransferMeta terrainMeta, PlacementLedger ledger,
            WorldDataDirectory heightSource, Generation selectedGeneration)
        {
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = terrainMeta.Origins.NoiseOrigin.x;
            gridConfig.worldOffsetZ = terrainMeta.Origins.NoiseOrigin.y;
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(gridConfig);
            var visualSections = BiomeVisualSectionTable.Resolve(selectedGeneration, biomeTypes);
            var treeSurroundSpecies = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(gridConfig), biomeTypes);
            var debugLayerAddresses = PlateauDebugOverlayGate.IsEnabled(gridConfig) ? gridConfig.alpine.debugTerrainLayerAddressablePaths : Array.Empty<string>();
            var layerTable = SplatLayerTable.Build(gridConfig.shoreConfig.beachLayerAddressablePath, gridConfig.rockLayerAddressablePath,
                visualSections.MainLayerAddresses, visualSections.TextureConfigs, visualSections.SurroundTextureConfigs, treeSurroundSpecies, debugLayerAddresses);

            var sharedCache = SharedWorldCache.For(terrainMeta.WorldId);
            var cacheKey = TerrainVisualCacheKey.Compute(terrainMeta.GenerationMasterFingerprint, config.seed, terrainMeta.Origins,
                terrainMeta.TerrainResolution, WorldProvisioner.GeneratorVersion);
            var baker = new TileVisualBaker(gridConfig, biomeTypes, visualSections, layerTable, treeSurroundSpecies, ledger,
                heightSource, new TerrainVisualCache(sharedCache, cacheKey));

            return new Result(baker, gridConfig, layerTable.OrderedLayerAddresses);
        }
    }
}
