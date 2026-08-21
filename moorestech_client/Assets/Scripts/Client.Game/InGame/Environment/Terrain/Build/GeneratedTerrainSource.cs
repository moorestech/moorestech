using System;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     生成ワールドの地形をタイル単位で組み立てる素材一式。マスタ由来の設定とアセットは全タイルで共有し、
    ///     タイルごとに変わるのは転送バイナリ・ノイズ窓・そのタイルに立つmapObjectだけという構造をそのまま形にしたもの
    ///     The material set building a generated world's terrain tile by tile; master-derived config and assets are shared
    ///     across tiles, mirroring that only the binaries, the noise window and the tile's own map objects differ
    /// </summary>
    public class GeneratedTerrainSource
    {
        private readonly TerrainLayer[] _terrainLayers;
        private readonly TileVisualBaker _visualBaker;
        private readonly List<DetailPrototype> _detailPrototypes;

        // ノイズ窓の原点をindex(0,0)タイルに合わせたConfig。他のタイルはここからタイル座標ぶんずらして作る
        // The config whose noise window origin sits on the index (0,0) tile; every other tile shifts off it by its tile coordinate
        private readonly TerrainGenerationConfig _config;

        private GeneratedTerrainSource(
            TerrainGenerationConfig config, TerrainLayer[] terrainLayers,
            TileVisualBaker visualBaker, List<DetailPrototype> detailPrototypes)
        {
            _config = config;
            _terrainLayers = terrainLayers;
            _visualBaker = visualBaker;
            _detailPrototypes = detailPrototypes;
        }

        public static async UniTask<GeneratedTerrainSource> CreateAsync(
            TerrainTransferMeta terrainMeta, string terrainHash, IReadOnlyList<MapObjectLayoutMessagePack> mapObjects)
        {
            // 生成時と同じ手順でConfigを組み直す。seedとノイズ窓原点はworld.json由来の値をワイヤで受け取っている
            // Rebuild the config exactly as generation did; the seed and noise window origin arrive over the wire from world.json
            var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
            var config = GenerationRuntimeConfigFactory.Build(selectedGeneration);
            config.seed = terrainMeta.WorldSeed;

            // マスタのworldOffsetはスポーン探索の中央化オフセットを含まない。そのまま使うと約2km離れた別の窓を分類することになる
            // The master worldOffset lacks the spawn-search centering offset; using it would classify a different window ~2km away
            config.worldOffsetX = terrainMeta.Origins.NoiseOrigin.x;
            config.worldOffsetZ = terrainMeta.Origins.NoiseOrigin.y;

            // マスタを差し替えると解像度が動く。読み出し長がずれて全画素が1列ずつ流れるので黙って通さない
            // Swapping the master moves the resolution; the read length would shift every pixel by a column, so it never passes silently
            if (config.Resolution != terrainMeta.TerrainResolution)
                throw new InvalidOperationException(
                    $"[GeneratedTerrainSource] Generation master resolution {config.Resolution} disagrees with the transferred terrain resolution {terrainMeta.TerrainResolution}.");

            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var visualSections = BiomeVisualSectionTable.Resolve(selectedGeneration, biomeTypes);

            // 台帳への変換は移設期間だけの橋渡し(WireLayoutLedgerAdapter)を1回だけ通す
            // The conversion to the ledger runs once through the migration-only bridge (WireLayoutLedgerAdapter)
            var ledger = WireLayoutLedgerAdapter.Build(mapObjects);

            // 木の根元のレイヤーは有効バイオームの樹種から集める。列を確保しないまま塗るとインデックスが引けない
            // The tree root layers are gathered from the enabled biomes' species; painting without reserving their columns would find no index
            var treeSurroundSpecies = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), biomeTypes);

            // 列を確保する条件は塗る条件と同一。緩めると誰も塗らない列のTerrainLayerをAddressablesから読み込むことになる
            // The columns are reserved on exactly the painting condition; loosening it would load TerrainLayers for columns nobody paints
            var debugLayerAddresses = PlateauDebugOverlayGate.IsEnabled(config)
                ? config.alpine.debugTerrainLayerAddressablePaths
                : Array.Empty<string>();
            var layerTable = SplatLayerTable.Build(
                config.shoreConfig.beachLayerAddressablePath, config.rockLayerAddressablePath,
                visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, treeSurroundSpecies, debugLayerAddresses);

            var terrainLayers = await TerrainLayerAssetLoader.LoadAsync(layerTable.OrderedLayerAddresses);

            var worldCacheDirectory = WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(terrainMeta.WorldId));

            // 見た目はマスタ・地形バイナリ・ノイズ窓原点・seed・mapObject配置の派生物。その5つを畳んだキーで前回の結果を引き当てる
            // The visuals derive from the master, the terrain binaries, the noise window origin, the seed and the map object layout; a key folding those five finds the previous result
            var visualCache = new TerrainVisualCache(worldCacheDirectory, TerrainVisualCacheKey.Compute(
                MasterHolder.GenerationMaster.SourceJsonText, terrainHash,
                new Vector2(config.worldOffsetX, config.worldOffsetZ), config.seed,
                PlacementLedgerDigest.Compute(ledger.Placements)));

            var visualBaker = new TileVisualBaker(
                config, biomeTypes, visualSections, layerTable, treeSurroundSpecies, ledger,
                worldCacheDirectory, visualCache);

            // detailプロトタイプの解決とUnity DetailPrototypeへの組み立てはここで一度だけ行う
            // Resolving the detail assets and assembling the Unity DetailPrototypes happens once, right here
            var resolvedDetailAssets = await DetailAssetResolver.ResolveAsync(visualBaker.DetailPrototypes);
            var detailPrototypes = TerrainDetailPrototypeList.Build(visualBaker.DetailPrototypes, resolvedDetailAssets);

            return new GeneratedTerrainSource(config, terrainLayers, visualBaker, detailPrototypes);
        }

        // 割り当ての式はサーバーと同一の TerrainGenerationConfig 側に1本だけ置く。別式だと片方だけ隣タイルへずれる
        // The assignment lives once on TerrainGenerationConfig, shared with the server; a second formula shifts only one side by a tile
        public Vector3 TileWorldPosition(int tileX, int tileZ)
        {
            var tileScene = _config.TileScenePosition(tileX, tileZ);
            return new Vector3(tileScene.x, 0f, tileScene.y);
        }

        public async UniTask<TerrainData> CreateTerrainDataAsync(int tileX, int tileZ)
        {
            var bakedTile = _visualBaker.Bake(tileX, tileZ);

            // 密度マップはプロトタイプと1対1。数が食い違ったまま流すとSetDetailLayerが別の草を描く
            // Density maps pair one-to-one with prototypes; letting a mismatched count through would make SetDetailLayer draw the wrong plant
            if (_detailPrototypes.Count != bakedTile.DetailMaps.Count)
                throw new InvalidOperationException(
                    $"[GeneratedTerrainSource] Tile ({tileX}, {tileZ}) has {bakedTile.DetailMaps.Count} detail maps for {_detailPrototypes.Count} prototypes.");

            var tileConfig = _config.CreateTileConfig(tileX, tileZ);
            var tileVisual = new TerrainTileVisual(bakedTile.Alphamap, bakedTile.DetailMaps);
            return await TerrainDataAssembler.AssembleAsync(
                tileConfig, bakedTile.Heights, tileVisual, _detailPrototypes, _terrainLayers);
        }
    }
}
