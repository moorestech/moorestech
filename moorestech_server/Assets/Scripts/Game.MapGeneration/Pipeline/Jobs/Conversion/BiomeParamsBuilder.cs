using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Jobs
{
    // BiomeType と Config から BiomeParams の基礎値（共通デフォルト・分類優先度・海岸/境界設定）を組む。
    // Builds the base BiomeParams (common defaults, classify priority, shore/boundary settings).
    public static class BiomeParamsBuilder
    {
        public static BiomeParams CreateBaseParams(BiomeType biomeType,
            TerrainGenerationConfig config, BiomePlacementHelper helper)
        {
            var bp = CreateDefaultParams((int)biomeType);
            bp.classifyPriority = GetClassifyPriority(biomeType);
            bp.splatmapLayerIndex = helper.GetSplatmapLayerIndex(biomeType);

            var shore = helper.GetShoreConfig(biomeType);
            if (shore != null)
            {
                bp.waterMargin = shore.waterMargin;
                bp.shoreBeachElevation = shore.beachElevation;
                bp.beachThreshold = shore.beachThreshold;
                bp.deepSeaThreshold = shore.deepSeaThreshold;
                bp.sandBlendThreshold = shore.sandBlendThreshold;
                bp.rockFallbackLayerIndex = shore.rockFallbackLayerIndex;
            }

            var boundary = helper.GetBoundaryConfig();
            bp.heightBlendFastPathThreshold = boundary.heightBlendFastPathThreshold;
            bp.heightBlendMinWeight = boundary.heightBlendMinWeight;
            bp.boundaryNoiseSmoothstepWidth = boundary.boundaryNoiseSmoothstepWidth;
            bp.boundaryNoiseMidWeight = boundary.boundaryNoiseMidWeight;
            bp.boundaryNoiseHighWeight = boundary.boundaryNoiseHighWeight;

            if (biomeType == BiomeType.Alpine)
            {
                bp.enablePlateau = config.alpine.enablePlateau ? 1 : 0;
                bp.plateauSearchBaseRadius = config.alpine.plateauSearchBaseRadius;
                bp.plateauBoundaryBlend = config.alpine.plateauBoundaryBlend;
            }

            return bp;
        }

        public static BiomeParams CreateDefaultParams(int biomeType)
        {
            return new BiomeParams
            {
                enabled = 1,
                biomeType = biomeType,
                temperatureMin = 0f, temperatureMax = 1f,
                elevationMin = 0f, elevationMax = 1f,
                humidityMin = 0f, humidityMax = 1f,
                exponent = 1f,
                lacunarity = 2f,
                persistence = 0.5f,
                terraceHeight = 1f,
                valleySharpness = 1.5f,
            };
        }

        // 旧 IBiomeDefinition.ClassifyPriority と同一値。
        // Same values as the legacy IBiomeDefinition.ClassifyPriority.
        public static int GetClassifyPriority(BiomeType type)
        {
            switch (type)
            {
                case BiomeType.Alpine: return 100;
                case BiomeType.Mesa: return 90;
                case BiomeType.Jungle: return 80;
                case BiomeType.Desert: return 70;
                case BiomeType.Forest: return 60;
                case BiomeType.Woods: return 55;
                case BiomeType.Savanna: return 50;
                case BiomeType.Grassland: return 0;
                default: return 0;
            }
        }

        // Classify 条件（温度/標高/湿度の閾値）を BiomeParams の min/max 範囲に変換する。
        // Convert classify thresholds (temperature/elevation/humidity) into BiomeParams min/max ranges.
        public static void FillClassificationRange(
            ref BiomeParams bp, TerrainGenerationConfig config, BiomeType type)
        {
            switch (type)
            {
                case BiomeType.Grassland: break;
                case BiomeType.Forest:
                    bp.humidityMin = config.forest.humidityThreshold;
                    bp.humidityMax = 1f;
                    break;
                case BiomeType.Savanna:
                    bp.temperatureMin = config.savanna.temperatureThreshold;
                    bp.temperatureMax = 1f;
                    break;
                case BiomeType.Desert:
                    bp.temperatureMin = 0f;
                    bp.temperatureMax = config.desert.temperatureThreshold;
                    break;
                case BiomeType.Mesa:
                    bp.elevationMin = config.mesa.elevationThreshold;
                    bp.elevationMax = 1f;
                    bp.humidityMin = 0f;
                    bp.humidityMax = config.mesa.humidityThreshold;
                    break;
                case BiomeType.Alpine:
                    bp.elevationMin = config.alpine.elevationThreshold;
                    bp.elevationMax = 1f;
                    break;
                case BiomeType.Jungle:
                    bp.temperatureMin = config.jungle.temperatureThreshold;
                    bp.temperatureMax = 1f;
                    bp.humidityMin = config.jungle.humidityThreshold;
                    bp.humidityMax = 1f;
                    break;
                case BiomeType.Woods:
                    bp.humidityMin = config.woods.humidityThreshold;
                    bp.humidityMax = config.woods.humidityUpperThreshold;
                    break;
            }
        }
    }
}
