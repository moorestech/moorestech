using System;
using Game.MapGeneration.Pipeline.Config;
using Mooresmaster.Model.GenerationModule;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // マスタ(GenerationModule)→ 実行時 TerrainGenerationConfig POCO の単一アダプタ入口。
    // ジェネレーター群はこの一時 POCO を消費し、真実源はマスタのまま（SSOT 非違反の materialize 層）。
    // Single adapter entry from the GenerationModule master to the runtime TerrainGenerationConfig
    // POCO; generators consume this transient view while the master stays the source of truth.
    public static class GenerationRuntimeConfigFactory
    {
        public static TerrainGenerationConfig Build(Generation generation)
        {
            // VanillaGenerator 以外はサーバー生成器を提供しないため即例外（呼び出し前提の検証）。
            // Non-VanillaGenerator provides no server generator, so fail fast (precondition check).
            if (generation.AlgorithmParam is not VanillaGeneratorAlgorithmParam vp)
                throw new InvalidOperationException(
                    "GenerationRuntimeConfigFactory requires a VanillaGenerator algorithmParam.");

            var cfg = new TerrainGenerationConfig();

            // スポーン候補探索
            // Spawn candidate search
            cfg.useSpawnOffsetSearch = vp.UseSpawnOffsetSearch;
            var ss = vp.SpawnSearch; var sc = cfg.spawnSearch;
            sc.overrideSpawnScenePosition = ss.OverrideSpawnScenePosition;
            sc.spawnScenePosition = ss.SpawnScenePosition;
            sc.scanCellSize = ss.ScanCellSize;
            sc.scanExtent = ss.ScanExtent;
            sc.windowMargin = ss.WindowMargin;
            sc.maxDetailedResolution = ss.MaxDetailedResolution;
            sc.minGrasslandArea = ss.MinGrasslandArea;
            sc.minForestArea = ss.MinForestArea;
            sc.minBorderContact = ss.MinBorderContact;
            sc.grassClearanceMin = ss.GrassClearanceMin;
            sc.waterClearanceMin = ss.WaterClearanceMin;
            sc.wGrasslandArea = ss.WGrasslandArea;
            sc.wForestArea = ss.WForestArea;
            sc.wBorderContact = ss.WBorderContact;
            sc.wInland = ss.WInland;
            sc.topK = ss.TopK;
            sc.expandFactor = ss.ExpandFactor;
            sc.maxExpandIterations = ss.MaxExpandIterations;

            // 出力・地形サイズ・シード
            // Output / terrain size / seed
            cfg.resolutionPreset = RuntimeConvert.ToResolutionPreset(vp.ResolutionPreset);
            cfg.terrainHeight = vp.TerrainHeight;
            cfg.terrainWidth = vp.TerrainWidth;
            cfg.terrainLength = vp.TerrainLength;
            cfg.gridSizeX = vp.GridSizeX;
            cfg.gridSizeZ = vp.GridSizeZ;
            cfg.seed = vp.Seed;
            cfg.worldOffsetX = vp.WorldOffsetX;
            cfg.worldOffsetZ = vp.WorldOffsetZ;
            cfg.spawnWorldPosition = vp.SpawnWorldPosition;
            cfg.chunkPadding = vp.ChunkPadding;
            cfg.overrideResolution = vp.OverrideResolution;

            // 大陸性・浸食・海面
            // Continentalness / erosion / sea level
            cfg.continentalnessFrequency = vp.ContinentalnessFrequency;
            cfg.continentalnessOctaves = vp.ContinentalnessOctaves;
            cfg.continentalnessPersistence = vp.ContinentalnessPersistence;
            cfg.landThreshold = vp.LandThreshold;
            cfg.erosionFrequency = vp.ErosionFrequency;
            cfg.erosionOctaves = vp.ErosionOctaves;
            cfg.erosionStrength = vp.ErosionStrength;
            cfg.seaLevel = vp.SeaLevel;

            // 共通海岸線
            // Common shore
            var shore = vp.ShoreConfig; var shc = cfg.shoreConfig;
            shc.waterMargin = shore.WaterMargin;
            shc.beachElevation = shore.BeachElevation;
            shc.beachLandTextureRadius = shore.BeachLandTextureRadius;
            shc.beachLandTerrainRadius = shore.BeachLandTerrainRadius;
            shc.beachSeaTextureRadius = shore.BeachSeaTextureRadius;
            shc.beachSeaTerrainRadius = shore.BeachSeaTerrainRadius;
            shc.beachLayerAddressablePath = shore.BeachLayerAddressablePath;
            shc.beachThreshold = shore.BeachThreshold;
            shc.deepSeaThreshold = shore.DeepSeaThreshold;
            shc.sandBlendThreshold = shore.SandBlendThreshold;
            shc.rockFallbackLayerIndex = shore.RockFallbackLayerIndex;
            shc.minSeaRegionSize = shore.MinSeaRegionSize;

            // 共通境界
            // Common boundary
            var bnd = vp.BoundaryConfig; var bc = cfg.boundaryConfig;
            bc.textureBlendStrength = bnd.TextureBlendStrength;
            bc.heightBlendFastPathThreshold = bnd.HeightBlendFastPathThreshold;
            bc.heightBlendMinWeight = bnd.HeightBlendMinWeight;
            bc.blurRadiusDivisor = bnd.BlurRadiusDivisor;
            bc.boundaryNoiseSmoothstepWidth = bnd.BoundaryNoiseSmoothstepWidth;
            bc.boundaryNoiseMidWeight = bnd.BoundaryNoiseMidWeight;
            bc.boundaryNoiseHighWeight = bnd.BoundaryNoiseHighWeight;

            // バイオーム分布ノイズ・ボロノイ・境界ワープ
            // Biome distribution noise / voronoi / boundary warp
            cfg.biomeScale = vp.BiomeScale;
            cfg.biomeBlendRadius = vp.BiomeBlendRadius;
            cfg.voronoiCellSize = vp.VoronoiCellSize;
            cfg.voronoiJitter = vp.VoronoiJitter;
            cfg.boundaryWarpOctaves = vp.BoundaryWarpOctaves;
            cfg.boundaryWarpStrength = vp.BoundaryWarpStrength;
            cfg.boundaryWarpFrequency = vp.BoundaryWarpFrequency;
            cfg.boundaryNoiseAmount = vp.BoundaryNoiseAmount;
            cfg.boundaryNoiseFrequency = vp.BoundaryNoiseFrequency;
            cfg.rockLayerAddressablePath = vp.RockLayerAddressablePath;

            // 生成レイヤーのオン/オフ・バイオーム有効フラグ
            // Generation layer toggles / biome enable flags
            cfg.generateHeightmap = vp.GenerateHeightmap;
            cfg.generateTexture = vp.GenerateTexture;
            cfg.generateDetail = vp.GenerateDetail;
            cfg.generateObject = vp.GenerateObject;
            cfg.generateOre = vp.GenerateOre;
            cfg.grasslandEnabled = vp.GrasslandEnabled;
            cfg.forestEnabled = vp.ForestEnabled;
            cfg.savannaEnabled = vp.SavannaEnabled;
            cfg.desertEnabled = vp.DesertEnabled;
            cfg.mesaEnabled = vp.MesaEnabled;
            cfg.alpineEnabled = vp.AlpineEnabled;
            cfg.jungleEnabled = vp.JungleEnabled;
            cfg.woodsEnabled = vp.WoodsEnabled;

            // 鉱脈・バイオーム定義
            // Veins / biome definitions
            cfg.oreConfig = OreRuntimeConfigFactory.Build(vp);
            BiomeRuntimeConfigFactory.Apply(cfg, vp);
            AlpineMesaRuntimeConfigFactory.Apply(cfg, vp);

            return cfg;
        }
    }
}
