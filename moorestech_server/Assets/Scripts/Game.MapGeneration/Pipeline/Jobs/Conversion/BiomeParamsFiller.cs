using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Jobs
{
    // 各 *BiomeConfig の高さ生成フィールドを BiomeParams に充填する。旧 IBiomeDefinition の
    // 数値マッピングをそのまま踏襲し、フィールド流用（exponent 等）も保存する。
    // Fills BiomeParams height fields from each *BiomeConfig, preserving the original numeric
    // mapping (including field-reuse such as exponent) verbatim.
    public static class BiomeParamsFiller
    {
        public static void FillHeightParams(ref BiomeParams bp, TerrainGenerationConfig config, BiomeType type)
        {
            switch (type)
            {
                case BiomeType.Grassland: FillGrassland(ref bp, config.grassland); break;
                case BiomeType.Forest:    FillForest(ref bp, config.forest); break;
                case BiomeType.Savanna:   FillSavanna(ref bp, config.savanna); break;
                case BiomeType.Desert:    FillDesert(ref bp, config.desert); break;
                case BiomeType.Mesa:      FillMesa(ref bp, config.mesa); break;
                case BiomeType.Alpine:    FillAlpine(ref bp, config.alpine); break;
                case BiomeType.Jungle:    FillJungle(ref bp, config.jungle); break;
                case BiomeType.Woods:     FillWoods(ref bp, config.woods); break;
            }
        }

        static void FillGrassland(ref BiomeParams bp, GrasslandBiomeConfig c)
        {
            bp.baseHeight = c.baseHeight;
            bp.hillAmplitude = c.hillAmplitude;
            bp.frequency = c.frequency;
            bp.amplitude = c.amplitude;
            bp.secondaryFrequency = c.detailFrequency;
            bp.secondaryAmplitude = c.detailAmplitude;
        }

        static void FillForest(ref BiomeParams bp, ForestBiomeConfig c)
        {
            bp.baseHeight = c.baseHeight;
            bp.hillAmplitude = c.amplitude;
            bp.exponent = c.exponent;
            bp.hillThreshold = c.lowlandCutoff;
            bp.frequency = c.baseFrequency;
            bp.octaves = c.baseOctaves;
            bp.persistence = c.basePersistence;
            bp.lacunarity = 2f;
            bp.secondaryFrequency = c.detailFrequency;
            bp.secondaryAmplitude = c.detailWeight;
            bp.canyonOctaves = c.detailOctaves;
            bp.domainWarpStrength = c.warpStrength;
            bp.domainWarpIterations = c.warpIterations;
            bp.plateauFlatten = c.plateauFlatten;
            bp.ridgeBlend = c.ridgeBlend;
            bp.ridgeOctaves = c.ridgeOctaves;
        }

        static void FillSavanna(ref BiomeParams bp, SavannaBiomeConfig c)
        {
            bp.baseHeight = c.baseHeight;
            bp.hillAmplitude = c.amplitude;
            bp.frequency = c.frequency;
            bp.octaves = 4;
            bp.persistence = 0.5f;
            bp.lacunarity = 2f;
            bp.hillThreshold = c.hillThreshold;
            bp.secondaryFrequency = c.plateauFrequency;
            bp.exponent = c.undulationAmplitude;
            bp.terraceSteps = c.plateauSharpness;
        }

        static void FillDesert(ref BiomeParams bp, DesertBiomeConfig c)
        {
            bp.baseHeight = c.baseHeight;
            bp.hillAmplitude = c.duneAmplitude + c.cliffAmplitude;
            bp.frequency = c.duneNoiseFrequency;
            bp.octaves = 3;
            bp.persistence = 0.5f;
            bp.lacunarity = 2f;
            bp.canyonDepth = c.canyonDepth;
            bp.canyonFreqMult = c.canyonFrequency / Math.Max(c.duneNoiseFrequency, 0.0001f);
            bp.canyonOctaves = c.canyonOctaves;
            bp.ridgeBlend = c.cliffAmplitude;
            bp.ridgeOctaves = c.cliffOctaves;
            bp.secondaryAmplitude = c.duneAmplitude;
            bp.secondaryFrequency = c.cliffFrequency;
            bp.absSmoothing = c.absSmoothing;
        }

        static void FillMesa(ref BiomeParams bp, MesaBiomeConfig c)
        {
            bp.baseHeight = c.baseHeight;
            bp.hillAmplitude = c.amplitude;
            bp.frequency = c.frequency;
            bp.octaves = c.octaves;
            bp.persistence = c.persistence;
            bp.lacunarity = 2f;
            bp.domainWarpStrength = c.warpStrength;
            bp.domainWarpIterations = c.warpIterations;
            bp.canyonDepth = c.canyonDepth;
            bp.canyonFreqMult = c.canyonFreqMult;
            bp.canyonOctaves = c.canyonOctaves;
            bp.secondaryFrequency = c.isolationFreqMult;
            bp.terraceBoundaryNoiseStrength = c.boundaryNoiseStrength;
            bp.terraceBoundaryFreqMult = c.boundaryNoiseFreqMult;
            bp.terraceBoundaryOctaves = c.boundaryNoiseOctaves;
            bp.hillThreshold = c.butteThreshold;
            bp.valleySharpness = c.cliffSteepness;
            bp.terraceSteps = c.terraceSteps;
            bp.terraceSharpness = c.terraceSharpness;
            bp.plateauFlatten = c.plateauFlatten;
            bp.secondaryAmplitude = c.floorVariation;
            bp.ridgeBlend = c.topNoiseStrength;
            bp.exponent = c.topNoiseFreqMult;
        }

        static void FillAlpine(ref BiomeParams bp, AlpineBiomeConfig c)
        {
            bp.baseHeight = c.baseHeight;
            bp.hillAmplitude = c.amplitude;
            bp.frequency = c.frequency;
            bp.octaves = c.octaves;
            bp.persistence = 0.45f;
            bp.lacunarity = 2f;
            bp.domainWarpStrength = c.warpStrength;
            bp.domainWarpIterations = c.warpIterations;
            bp.ridgeBlend = c.ridgeBlend;
            bp.ridgeOctaves = c.ridgeOctaves;
            bp.exponent = c.exponent;
            bp.plateauFlatten = c.ceilStrength;
            bp.secondaryFrequency = c.ceilHeight;
            bp.floorHeight = c.floorHeight;
            bp.floorStrength = c.floorStrength;
        }

        static void FillJungle(ref BiomeParams bp, JungleBiomeConfig c)
        {
            bp.baseHeight = c.baseHeight;
            bp.hillAmplitude = c.amplitude;
            bp.frequency = c.terraceFrequency;
            bp.octaves = c.warpOctaves;
            bp.terraceSteps = c.terraceStepCount;
            bp.domainWarpStrength = c.warpStrength;
            bp.ridgeBlend = c.cellHeightVariation;
            bp.absSmoothing = c.slopeWidth;
            bp.secondaryFrequency = c.slopeRepeat;
            bp.secondaryAmplitude = c.slopeCoverage;
            bp.plateauFlatten = c.surfaceDetailAmplitude;
            bp.exponent = c.surfaceDetailFrequency;
            bp.terraceSharpness = c.transitionSmoothing;
        }

        static void FillWoods(ref BiomeParams bp, WoodsBiomeConfig c)
        {
            bp.baseHeight = c.baseHeight;
            bp.hillAmplitude = c.amplitude;
            bp.frequency = c.frequency;
            bp.octaves = 4;
            bp.persistence = 0.5f;
            bp.lacunarity = 2f;
            bp.terraceEnabled = 1;
            bp.terraceSteps = c.terraceSteps;
            bp.terraceSharpness = c.terraceSharpness;
        }
    }
}
