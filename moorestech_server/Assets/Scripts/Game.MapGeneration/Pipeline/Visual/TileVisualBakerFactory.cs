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
using Game.MapGeneration.Transfer;
using Game.Paths;
using Mooresmaster.Model.GenerationModule;

namespace Game.MapGeneration.Pipeline.Visual
{
    /// <summary>
    ///     ワールド同一性(転送メタ)から TileVisualBaker を組み立てる唯一の窓口。クライアントの WorldTerrainSession.Open と
    ///     サーバーの先焼き(TerrainVisualPrebake)が同じ組み立てを通るための切り出し
    ///     入口を用途別に2本置き、両者の唯一の違いである高さ源の決定をここが持つ（呼び出し元は選ばない）
    ///     The single window building a TileVisualBaker from a world identity (transfer meta), so the client's
    ///     WorldTerrainSession.Open and the server's prebake (TerrainVisualPrebake) share the very same assembly
    ///     Two entries, one per use, keep the height-source decision (their only difference) here rather than at the callers
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

        // クライアントは自分の地形ファイルを持たない。高さ源は先焼きが書いた共有キャッシュで、その導出は呼び出し元に持たせない
        // A client owns no terrain files of its own: its height source is the shared cache the prebake wrote, and deriving it never falls to the caller
        public static Result CreateForClient(
            TerrainGenerationConfig config, TerrainTransferMeta terrainMeta, Generation selectedGeneration)
        {
            var ledgerSource = new RegeneratedPlacementLedgerSource(selectedGeneration, config);
            return CreateWithHeightSource(
                config, terrainMeta, ledgerSource, selectedGeneration, SharedCacheOf(terrainMeta));
        }

        // 先焼きの高さ源はワールド本体のterrain/(生成した本人が唯一の正)。共有キャッシュへの複製は要らない
        // The prebake's height source is the world's own terrain/ (the generator itself is the sole truth); no copy into the shared cache is needed
        public static Result CreateForPrebake(
            TerrainGenerationConfig config, TerrainTransferMeta terrainMeta,
            PlacementLedger ledger, Generation selectedGeneration, WorldDataDirectory worldDataDirectory)
        {
            var ledgerSource = new MaterializedPlacementLedgerSource(ledger);
            return CreateWithHeightSource(
                config, terrainMeta, ledgerSource, selectedGeneration, worldDataDirectory);
        }

        private static Result CreateWithHeightSource(
            TerrainGenerationConfig config, TerrainTransferMeta terrainMeta,
            IPlacementLedgerSource ledgerSource, Generation selectedGeneration, WorldDataDirectory heightSource)
        {
            // payloadは常にメタの持ち物。別引数で受けると不整合な対を組める余地が残るのでここで1度だけ読む
            // The payload always belongs to the meta; taking it as a separate argument would allow an inconsistent pair, so it is read here once
            var generatedPayload = terrainMeta.GeneratedPayload;
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = generatedPayload.Origins.NoiseOrigin.x;
            gridConfig.worldOffsetZ = generatedPayload.Origins.NoiseOrigin.y;
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(gridConfig);
            var visualSections = BiomeVisualSectionTable.Resolve(selectedGeneration, biomeTypes);
            var treeSurroundSpecies = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(gridConfig), biomeTypes);

            var debugLayerAddresses = PlateauDebugOverlayGate.IsEnabled(gridConfig) ? gridConfig.alpine.debugTerrainLayerAddressablePaths : Array.Empty<string>();
            var layerTable = SplatLayerTable.Build(gridConfig.shoreConfig.beachLayerAddressablePath, gridConfig.rockLayerAddressablePath,
                visualSections.MainLayerAddresses, visualSections.TextureConfigs, visualSections.SurroundTextureConfigs, treeSurroundSpecies, debugLayerAddresses);

            // 台帳の指紋はワールド作成時に確定して転送メタが運ぶ。鍵のためだけにpass-1を回さない
            // The ledger digest is settled at world creation and carried by the transfer meta, so the key alone never triggers a pass-1
            var cacheKey = TerrainVisualCacheKey.Compute(
                generatedPayload.GenerationMasterFingerprint, config.seed, generatedPayload.Origins,
                terrainMeta.TerrainResolution, generatedPayload.GeneratorVersion, generatedPayload.PlacementLedgerDigest);
            var baker = new TileVisualBaker(gridConfig, biomeTypes, visualSections, layerTable, treeSurroundSpecies, ledgerSource,
                generatedPayload.PlacementLedgerDigest, heightSource, new TerrainVisualCache(SharedCacheOf(terrainMeta), cacheKey));

            return new Result(baker, gridConfig, layerTable.OrderedLayerAddresses);
        }

        // 見た目キャッシュの置き場はワールドIDから一意に決まる。導出をこの1行に閉じ、規則を呼び出し元へ漏らさない
        // A world id settles the visual cache location uniquely; closing the derivation into this single line keeps the rule out of the callers
        private static WorldDataDirectory SharedCacheOf(TerrainTransferMeta terrainMeta)
        {
            return WorldDataDirectory.ForWorldCache(terrainMeta.WorldId);
        }

    }
}
