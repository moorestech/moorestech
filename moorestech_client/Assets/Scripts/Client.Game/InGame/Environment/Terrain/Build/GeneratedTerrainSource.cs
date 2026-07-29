using System;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Stages;
using Game.Paths;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     生成ワールドの地形をタイル単位で組み立てる素材一式。マスタ由来の設定とアセットは全タイルで共有し、
    ///     タイルごとに変わるのは転送バイナリだけという構造をそのまま形にしたもの
    ///     The material set building a generated world's terrain tile by tile; master-derived config and assets are
    ///     shared across tiles, mirroring that only the transferred binaries differ per tile
    /// </summary>
    public class GeneratedTerrainSource
    {
        // 移植元と同じパッチ解像度。SetDetailResolutionの第2引数で描画パッチの粒度を決める
        // The source's patch resolution; SetDetailResolution's second argument sets the render patch granularity
        private const int DetailResolutionPerPatch = 16;

        private readonly BiomeType[] _biomeTypes;
        private readonly TerrainGenerationConfig _config;
        private readonly SplatLayerTable _layerTable;
        private readonly TerrainLayer[] _terrainLayers;
        private readonly BiomeVisualSections _visualSections;
        private readonly WorldDataDirectory _worldCacheDirectory;

        // 地形をシーンへ置く原点。config.worldOffsetはノイズ窓の原点なので設置には使えない
        // Scene origin the terrain is placed at; config.worldOffset is the noise window origin and cannot serve as a position
        private readonly Vector2 _sceneOrigin;

        private GeneratedTerrainSource(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, BiomeVisualSections visualSections,
            SplatLayerTable layerTable, TerrainLayer[] terrainLayers, WorldDataDirectory worldCacheDirectory,
            Vector2 sceneOrigin)
        {
            _sceneOrigin = sceneOrigin;
            _config = config;
            _biomeTypes = biomeTypes;
            _visualSections = visualSections;
            _layerTable = layerTable;
            _terrainLayers = terrainLayers;
            _worldCacheDirectory = worldCacheDirectory;
        }

        public static async UniTask<GeneratedTerrainSource> CreateAsync(GetMapDataProtocol.ResponseMapDataMessagePack mapLayout)
        {
            // 生成時と同じ手順でConfigを組み直す。seedとノイズ窓原点はworld.json由来の値をワイヤで受け取っている
            // Rebuild the config exactly as generation did; the seed and noise window origin arrive over the wire from world.json
            var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
            var config = GenerationRuntimeConfigFactory.Build(selectedGeneration);
            config.seed = mapLayout.WorldSeed;

            // マスタのworldOffsetはスポーン探索の中央化オフセットを含まない。そのまま使うと約2km離れた別の窓を分類することになる
            // The master worldOffset lacks the spawn-search centering offset; using it would classify a different window ~2km away
            config.worldOffsetX = mapLayout.TerrainNoiseOriginX;
            config.worldOffsetZ = mapLayout.TerrainNoiseOriginZ;
            var sceneOrigin = new Vector2(mapLayout.TerrainSceneOriginX, mapLayout.TerrainSceneOriginZ);

            // マスタを差し替えると解像度が動く。読み出し長がずれて全画素が1列ずつ流れるので黙って通さない
            // Swapping the master moves the resolution; the read length would shift every pixel by a column, so it never passes silently
            if (config.Resolution != mapLayout.TerrainResolution)
                throw new InvalidOperationException(
                    $"[GeneratedTerrainSource] Generation master resolution {config.Resolution} disagrees with the transferred terrain resolution {mapLayout.TerrainResolution}.");

            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var visualSections = BiomeVisualSectionTable.Resolve(selectedGeneration, biomeTypes);
            var layerTable = SplatLayerTable.Build(
                config.shoreConfig.beachLayerAddressablePath, config.rockLayerAddressablePath,
                visualSections.MainLayerAddresses, visualSections.TextureConfigs);

            var terrainLayers = await TerrainLayerAssetLoader.LoadAsync(layerTable.OrderedLayerAddresses);
            await DetailAssetResolver.ResolveAsync(visualSections.DetailConfigs);

            var worldCacheDirectory = WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(mapLayout.WorldId));
            return new GeneratedTerrainSource(config, biomeTypes, visualSections, layerTable, terrainLayers, worldCacheDirectory, sceneOrigin);
        }

        // タイルはシーン原点を起点に地形1枚ぶんずつ並ぶ。MapObjects/MapVeinsも同じ原点で配られる
        // Tiles are laid out one terrain apart from the scene origin, the same origin MapObjects/MapVeins are served in
        public Vector3 TileWorldPosition(int tileX, int tileZ)
        {
            return new Vector3(
                _sceneOrigin.x + tileX * _config.terrainWidth, 0f,
                _sceneOrigin.y + tileZ * _config.terrainLength);
        }

        public TerrainData CreateTerrainData(int tileX, int tileZ)
        {
            var resolution = _config.Resolution;
            var heights = TerrainFileLoader.LoadHeights(_worldCacheDirectory, tileX, tileZ, resolution);
            var transferredBiomeIndices = TerrainFileLoader.LoadBiomeIndices(_worldCacheDirectory, tileX, tileZ, resolution);

            var alphamap = SplatmapRuntimeGenerator.Generate(
                _config, _biomeTypes, _layerTable, _visualSections.TextureConfigs, _visualSections.MainLayerAddresses,
                heights, transferredBiomeIndices, _config.AlphamapResolution);
            var (detailPrototypes, detailMaps) = TerrainDetailBuilder.Build(
                _config, _biomeTypes, _visualSections, heights, transferredBiomeIndices, alphamap, _terrainLayers);

            var terrainData = new TerrainData();
            ApplyHeightmap();
            ApplySplatmap();
            ApplyDetail();
            return terrainData;

            #region Internal

            // heightmapResolutionを先に入れる。後から変えるとsizeもSetHeightsの結果も作り直される
            // heightmapResolution comes first: changing it afterwards rebuilds both size and the SetHeights result
            void ApplyHeightmap()
            {
                terrainData.heightmapResolution = resolution;
                terrainData.size = new Vector3(_config.terrainWidth, _config.terrainHeight, _config.terrainLength);
                terrainData.SetHeights(0, 0, heights);
            }

            void ApplySplatmap()
            {
                terrainData.alphamapResolution = alphamap.GetLength(0);
                terrainData.terrainLayers = _terrainLayers;
                terrainData.SetAlphamaps(0, 0, alphamap);
            }

            void ApplyDetail()
            {
                if (detailPrototypes.Count == 0) return;

                terrainData.SetDetailResolution(detailMaps[0].GetLength(0), DetailResolutionPerPatch);

                // CoverageModeではメッシュDetailが描画されないことがあるため移植元と同じくInstanceCountModeにする
                // CoverageMode can leave mesh details undrawn, so InstanceCountMode is used as in the source
                terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                terrainData.detailPrototypes = detailPrototypes.ToArray();

                for (var layerIndex = 0; layerIndex < detailMaps.Count; layerIndex++)
                    terrainData.SetDetailLayer(0, 0, layerIndex, detailMaps[layerIndex]);
            }

            #endregion
        }
    }
}
