using System;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Client.Game.InGame.Environment.Terrain.Visual.Cache;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
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
        private readonly BiomeType[] _biomeTypes;
        private readonly SplatLayerTable _layerTable;
        private readonly TerrainLayer[] _terrainLayers;
        private readonly TerrainVisualCache _visualCache;
        private readonly BiomeVisualSections _visualSections;
        private readonly WorldDataDirectory _worldCacheDirectory;

        // ノイズ窓の原点をindex(0,0)タイルに合わせたConfig。他のタイルはここからタイル座標ぶんずらして作る
        // The config whose noise window origin sits on the index (0,0) tile; every other tile shifts off it by its tile coordinate
        private readonly TerrainGenerationConfig _config;

        // シーン絶対座標のまま全タイルぶんを持つ。切り出しはタイル毎にTileMapObjectSlicerが行う
        // Held whole in scene-absolute coordinates; TileMapObjectSlicer carves out each tile's share
        private readonly IReadOnlyList<MapObjectLayoutMessagePack> _mapObjects;

        // 地形をシーンへ置く原点。config.worldOffsetはノイズ窓の原点なので設置には使えない
        // Scene origin the terrain is placed at; config.worldOffset is the noise window origin and cannot serve as a position
        private readonly Vector2 _sceneOrigin;

        private GeneratedTerrainSource(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, BiomeVisualSections visualSections,
            SplatLayerTable layerTable, TerrainLayer[] terrainLayers, WorldDataDirectory worldCacheDirectory,
            Vector2 sceneOrigin, IReadOnlyList<MapObjectLayoutMessagePack> mapObjects, TerrainVisualCache visualCache)
        {
            _visualCache = visualCache;
            _sceneOrigin = sceneOrigin;
            _mapObjects = mapObjects;
            _config = config;
            _biomeTypes = biomeTypes;
            _visualSections = visualSections;
            _layerTable = layerTable;
            _terrainLayers = terrainLayers;
            _worldCacheDirectory = worldCacheDirectory;
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
            var sceneOrigin = terrainMeta.Origins.SceneOrigin;

            // マスタを差し替えると解像度が動く。読み出し長がずれて全画素が1列ずつ流れるので黙って通さない
            // Swapping the master moves the resolution; the read length would shift every pixel by a column, so it never passes silently
            if (config.Resolution != terrainMeta.TerrainResolution)
                throw new InvalidOperationException(
                    $"[GeneratedTerrainSource] Generation master resolution {config.Resolution} disagrees with the transferred terrain resolution {terrainMeta.TerrainResolution}.");

            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var visualSections = BiomeVisualSectionTable.Resolve(selectedGeneration, biomeTypes);
            var layerTable = SplatLayerTable.Build(
                config.shoreConfig.beachLayerAddressablePath, config.rockLayerAddressablePath,
                visualSections.MainLayerAddresses, visualSections.TextureConfigs);

            var terrainLayers = await TerrainLayerAssetLoader.LoadAsync(layerTable.OrderedLayerAddresses);
            await DetailAssetResolver.ResolveAsync(visualSections.DetailConfigs);

            var worldCacheDirectory = WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(terrainMeta.WorldId));

            // 見た目はマスタ・地形バイナリ・ノイズ窓原点・seed・mapObject配置の派生物。その5つを畳んだキーで前回の結果を引き当てる
            // The visuals derive from the master, the terrain binaries, the noise window origin, the seed and the map object layout; a key folding those five finds the previous result
            var visualCache = new TerrainVisualCache(worldCacheDirectory, TerrainVisualCacheKey.Compute(
                MasterHolder.GenerationMaster.SourceJsonText, terrainHash,
                new Vector2(config.worldOffsetX, config.worldOffsetZ), config.seed,
                MapObjectsDigest.Compute(mapObjects)));

            return new GeneratedTerrainSource(
                config, biomeTypes, visualSections, layerTable, terrainLayers, worldCacheDirectory, sceneOrigin,
                mapObjects, visualCache);
        }

        // タイルはシーン原点を起点に地形1枚ぶんずつ並ぶ。MapObjects/MapVeinsも同じ原点で配られる
        // Tiles are laid out one terrain apart from the scene origin, the same origin MapObjects/MapVeins are served in
        public Vector3 TileWorldPosition(int tileX, int tileZ)
        {
            return new Vector3(
                _sceneOrigin.x + tileX * _config.terrainWidth, 0f,
                _sceneOrigin.y + tileZ * _config.terrainLength);
        }

        // visualCacheHitは呼び出し側の計測用。1枚ごとに再構築を省けたかを返す
        // visualCacheHit reports, tile by tile, whether the rebuild was skipped, for the caller's measurement
        public async UniTask<(TerrainData TerrainData, bool VisualCacheHit)> CreateTerrainDataAsync(int tileX, int tileZ)
        {
            var resolution = _config.Resolution;

            // 転送された高さは木の摂動前が正本（R12）。splatとdetail密度はこの値を読み、表示だけが摂動後を使う
            // The transferred heights are pre-tree by definition (R12): splat and detail density read them and only the display uses the perturbed ones
            var preHeights = TerrainFileLoader.LoadHeights(_worldCacheDirectory, tileX, tileZ, resolution);
            var transferredBiomeIndices = TerrainFileLoader.LoadBiomeIndices(_worldCacheDirectory, tileX, tileZ, resolution);
            var detailPrototypes = TerrainDetailPrototypeList.Build(_biomeTypes, _visualSections);

            // サーバーのタイルループと同形にずらす。窓が1枚ぶんずれないと全25タイルが同じ地形として分類される
            // Shifted exactly as the server's tile loop does; without the per-tile shift all 25 tiles classify as the same terrain
            var tileConfig = _config.ShallowCopy();
            tileConfig.worldOffsetX = _config.worldOffsetX + tileX * _config.terrainWidth;
            tileConfig.worldOffsetZ = _config.worldOffsetZ + tileZ * _config.terrainLength;

            var tileObjects = TileMapObjectSlicer.Slice(
                _mapObjects, TileWorldPosition(tileX, tileZ), _config.terrainWidth, _config.terrainLength);
            var postHeights = TreePerturbationApplier.Apply(preHeights, tileConfig, tileObjects);

            // detailの解像度とプロトタイプ数を先に固定する。ヒット後に数違いで落とさず、Readerで壊れた取り逃しにする
            // Fix detail resolution and prototype count before loading so a mismatch becomes a broken miss in the Reader, never a post-hit failure
            var visualCacheHit = _visualCache.TryLoad(
                tileX, tileZ, _config.AlphamapResolution, _terrainLayers.Length, resolution - 1, detailPrototypes.Count,
                out var tileVisual);
            if (!visualCacheHit) tileVisual = RebuildAndCacheVisual();

            // 密度マップはプロトタイプと1対1。数が食い違ったまま流すとSetDetailLayerが別の草を描く
            // Density maps pair one-to-one with prototypes; letting a mismatched count through would make SetDetailLayer draw the wrong plant
            if (detailPrototypes.Count != tileVisual.DetailMaps.Count)
                throw new InvalidOperationException(
                    $"[GeneratedTerrainSource] Tile ({tileX}, {tileZ}) has {tileVisual.DetailMaps.Count} detail maps for {detailPrototypes.Count} prototypes.");

            var terrainData = await TerrainDataAssembler.AssembleAsync(
                tileConfig, postHeights, tileVisual, detailPrototypes, _terrainLayers);
            return (terrainData, visualCacheHit);

            #region Internal

            // 取り逃したタイルだけをその場で作り直し、次回のために書き戻す
            // Only the missed tiles are rebuilt on the spot and written back for next time
            TerrainTileVisual RebuildAndCacheVisual()
            {
                var rebuiltAlphamap = SplatmapRuntimeGenerator.Generate(
                    tileConfig, _biomeTypes, _layerTable, _visualSections.TextureConfigs, _visualSections.MainLayerAddresses,
                    preHeights, transferredBiomeIndices, _config.AlphamapResolution);
                var rebuiltDetailMaps = TerrainDetailBuilder.Build(
                    tileConfig, _biomeTypes, _visualSections, preHeights, postHeights, transferredBiomeIndices,
                    rebuiltAlphamap, _terrainLayers);

                var rebuiltVisual = new TerrainTileVisual(rebuiltAlphamap, rebuiltDetailMaps);
                _visualCache.Save(tileX, tileZ, rebuiltVisual);
                return rebuiltVisual;
            }

            #endregion
        }
    }
}
