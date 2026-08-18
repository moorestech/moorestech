using System;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Client.Game.InGame.Environment.Terrain.Visual.Cache;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Stages;
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
        private readonly TerrainTileVisualProvider _tileVisualProvider;
        private readonly WorldDataDirectory _worldCacheDirectory;

        // ノイズ窓の原点をindex(0,0)タイルに合わせたConfig。他のタイルはここからタイル座標ぶんずらして作る
        // The config whose noise window origin sits on the index (0,0) tile; every other tile shifts off it by its tile coordinate
        private readonly TerrainGenerationConfig _config;

        // シーン絶対座標のまま全タイルぶんを持つ。切り出しはタイル毎にTileMapObjectSlicerが行う
        // Held whole in scene-absolute coordinates; TileMapObjectSlicer carves out each tile's share
        private readonly IReadOnlyList<MapObjectLayoutMessagePack> _mapObjects;

        private GeneratedTerrainSource(
            TerrainGenerationConfig config, TerrainLayer[] terrainLayers, WorldDataDirectory worldCacheDirectory,
            IReadOnlyList<MapObjectLayoutMessagePack> mapObjects, TerrainTileVisualProvider tileVisualProvider)
        {
            _config = config;
            _terrainLayers = terrainLayers;
            _worldCacheDirectory = worldCacheDirectory;
            _mapObjects = mapObjects;
            _tileVisualProvider = tileVisualProvider;
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
            await DetailAssetResolver.ResolveAsync(visualSections.DetailConfigs);

            var worldCacheDirectory = WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(terrainMeta.WorldId));

            // 見た目はマスタ・地形バイナリ・ノイズ窓原点・seed・mapObject配置の派生物。その5つを畳んだキーで前回の結果を引き当てる
            // The visuals derive from the master, the terrain binaries, the noise window origin, the seed and the map object layout; a key folding those five finds the previous result
            var visualCache = new TerrainVisualCache(worldCacheDirectory, TerrainVisualCacheKey.Compute(
                MasterHolder.GenerationMaster.SourceJsonText, terrainHash,
                new Vector2(config.worldOffsetX, config.worldOffsetZ), config.seed,
                MapObjectsDigest.Compute(mapObjects)));

            var tileVisualProvider = new TerrainTileVisualProvider(
                config, biomeTypes, visualSections, layerTable, terrainLayers, treeSurroundSpecies,
                mapObjects, worldCacheDirectory, visualCache);

            return new GeneratedTerrainSource(
                config, terrainLayers, worldCacheDirectory, mapObjects, tileVisualProvider);
        }

        // 割り当ての式はサーバーと同一の TerrainGenerationConfig 側に1本だけ置く。別式だと片方だけ隣タイルへずれる
        // The assignment lives once on TerrainGenerationConfig, shared with the server; a second formula shifts only one side by a tile
        public Vector3 TileWorldPosition(int tileX, int tileZ)
        {
            var tileScene = _config.TileScenePosition(tileX, tileZ);
            return new Vector3(tileScene.x, 0f, tileScene.y);
        }

        // visualCacheHitは呼び出し側の計測用。1枚ごとに再構築を省けたかを返す
        // visualCacheHit reports, tile by tile, whether the rebuild was skipped, for the caller's measurement
        public async UniTask<(TerrainData TerrainData, bool VisualCacheHit)> CreateTerrainDataAsync(int tileX, int tileZ)
        {
            // 転送された高さは木の摂動前が正本（R12）。splatとdetail密度はこの値を読み、表示だけが摂動後を使う
            // The transferred heights are pre-tree by definition (R12): splat and detail density read them and only the display uses the perturbed ones
            var preHeights = TerrainFileLoader.LoadHeights(_worldCacheDirectory, tileX, tileZ, _config.Resolution);

            // サーバーのタイルループと同じ1本の式でずらす。窓が1枚ぶんずれないと全25タイルが同じ地形として分類される
            // Shifted by the very formula the server's tile loop uses; without the per-tile shift all 25 tiles classify as the same terrain
            var tileConfig = _config.CreateTileConfig(tileX, tileZ);

            var tileWorldPosition = TileWorldPosition(tileX, tileZ);
            var postHeights = TreePerturbationApplier.Apply(
                preHeights, tileConfig, tileWorldPosition, _mapObjects);

            var (tileVisual, visualCacheHit) = _tileVisualProvider.Resolve(
                tileX, tileZ, tileConfig, tileWorldPosition, preHeights, postHeights);

            // 密度マップはプロトタイプと1対1。数が食い違ったまま流すとSetDetailLayerが別の草を描く
            // Density maps pair one-to-one with prototypes; letting a mismatched count through would make SetDetailLayer draw the wrong plant
            var detailPrototypes = _tileVisualProvider.DetailPrototypes;
            if (detailPrototypes.Count != tileVisual.DetailMaps.Count)
                throw new InvalidOperationException(
                    $"[GeneratedTerrainSource] Tile ({tileX}, {tileZ}) has {tileVisual.DetailMaps.Count} detail maps for {detailPrototypes.Count} prototypes.");

            var terrainData = await TerrainDataAssembler.AssembleAsync(
                tileConfig, postHeights, tileVisual, detailPrototypes, _terrainLayers);
            return (terrainData, visualCacheHit);
        }
    }
}
