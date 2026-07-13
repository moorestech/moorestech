using System;
using System.Collections.Generic;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MapGenerator.Pipeline.Jobs
{
    /// <summary>
    /// マネージドConfig→NativeArrayの変換ブリッジ。
    /// BiomeType配列のソート順・RNG消費順序を厳守し、seed再現性を保証する。
    /// </summary>
    public static class JobDataConverter
    {
        /// <summary>
        /// 各*BiomeConfigからBiomeParamsに変換する。
        /// ClassifyPriority降順のソート順に従い、ジョブ側の評価順序と一致させる。
        /// </summary>
        public static NativeArray<BiomeParams> ConvertBiomeParams(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, Allocator allocator)
        {
            var helper = new BiomePlacementHelper(config);
            var result = new NativeArray<BiomeParams>(biomeTypes.Length, allocator);

            for (int i = 0; i < biomeTypes.Length; i++)
            {
                var bp = CreateBaseParams(biomeTypes[i], config, helper);
                FillHeightParams(ref bp, config, biomeTypes[i]);
                FillClassificationRange(ref bp, config, biomeTypes[i]);
                result[i] = bp;
            }

            return result;
        }

        /// <summary>
        /// 大陸性・浸食・バイオーム分類用のノイズオフセットをNativeArrayで返す。
        /// ClassificationJobに渡す。RNG消費順がGenerateNoiseOffsetsと一致していることが必須。
        /// </summary>
        public static void GenerateClassificationOffsets(
            TerrainGenerationConfig config,
            Allocator allocator,
            out NativeArray<float2> continentalnessOffsets,
            out NativeArray<float2> erosionOffsets)
        {
            var rng = new System.Random(config.seed);
            continentalnessOffsets = AllocOffsets(rng, config.continentalnessOctaves, allocator);
            erosionOffsets = AllocOffsets(rng, config.erosionOctaves, allocator);
        }

        /// <summary>
        /// ボロノイ四色定理配色用のバイオーム並び替えテーブルを生成する。
        /// Fisher-Yatesシャッフルでseed依存の決定論的置換を返す。
        /// </summary>
        public static NativeArray<int> GenerateBiomePermutation(
            int seed, int biomeCount, Allocator allocator)
        {
            var perm = new NativeArray<int>(biomeCount, allocator);
            for (int i = 0; i < biomeCount; i++) perm[i] = i;

            var rng = new System.Random(seed + 54321);
            for (int i = biomeCount - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = perm[i];
                perm[i] = perm[j];
                perm[j] = tmp;
            }
            return perm;
        }

        /// <summary>
        /// RNGからcount個のオフセットをNativeArrayとして生成する。
        /// </summary>
        static NativeArray<float2> AllocOffsets(System.Random rng, int count, Allocator allocator)
        {
            var arr = new NativeArray<float2>(count, allocator);
            for (int i = 0; i < count; i++)
            {
                arr[i] = new float2(
                    (float)rng.NextDouble() * 10000f,
                    (float)rng.NextDouble() * 10000f);
            }
            return arr;
        }

        /// <summary>
        /// 既存パイプラインと同じRNG消費順でnoiseOffsetsを生成し、flat NativeArrayに詰める。
        /// 各biomeParamsのnoiseOffsetBase/Countはここで設定される。
        /// </summary>
        public static NativeArray<float2> GenerateNoiseOffsets(
            TerrainGenerationConfig config,
            NativeArray<BiomeParams> biomeParams,
            BiomeType[] biomeTypes,
            Allocator allocator)
        {
            // 分類オフセットのRNG消費をスキップ（GenerateClassificationOffsetsと同じ順序）
            var rng = new System.Random(config.seed);
            ConsumeOffsets(rng, config.continentalnessOctaves);  // continentalnessOffsets
            ConsumeOffsets(rng, config.erosionOctaves);           // erosionOffsets
            ConsumeOffsets(rng, 4);  // biomeTempOffsets
            ConsumeOffsets(rng, 4);  // biomeElevOffsets
            ConsumeOffsets(rng, 4);  // biomeHumidOffsets

            // 全バイオームの合計オフセット数を算出してフラット配列を確保
            int totalOffsets = 0;
            for (int i = 0; i < biomeTypes.Length; i++)
                totalOffsets += GetNoiseOffsetCount(config, biomeTypes[i]);

            var result = new NativeArray<float2>(totalOffsets, allocator);
            int cursor = 0;

            // ソート後配列と同順でRNGを消費（seed再現性の鍵）
            for (int i = 0; i < biomeTypes.Length; i++)
            {
                int count = GetNoiseOffsetCount(config, biomeTypes[i]);

                // biomeParamsにスライス情報を書き戻す
                var bp = biomeParams[i];
                bp.noiseOffsetBase = cursor;
                bp.noiseOffsetCount = count;
                biomeParams[i] = bp;

                // RNGからオフセットを生成してフラット配列に格納
                for (int j = 0; j < count; j++)
                {
                    result[cursor + j] = new float2(
                        (float)rng.NextDouble() * 10000f,
                        (float)rng.NextDouble() * 10000f
                    );
                }
                cursor += count;
            }

            return result;
        }

        /// <summary>
        /// 各バイオームのBiomeTextureConfig.entriesをフラットなNativeArrayに変換する。
        /// biomeParamsのtextureEntryBase/Countはここで設定される。
        /// </summary>
        public static NativeArray<TextureEntryParams> ConvertTextureEntries(
            TerrainGenerationConfig config,
            NativeArray<BiomeParams> biomeParams,
            BiomePlacementHelper helper,
            BiomeType[] biomeTypes,
            Dictionary<TerrainLayer, int> layerIndexMap,
            Allocator allocator)
        {
            // テクスチャノイズ用RNG。既存SplatmapGeneratorと同じseedオフセット
            var textureRng = new System.Random(config.seed + 77777);

            // 合計エントリ数を算出
            int totalEntries = 0;
            for (int i = 0; i < biomeTypes.Length; i++)
            {
                var texConfig = helper.GetTextureConfig(biomeTypes[i]);
                if (texConfig?.entries != null)
                    totalEntries += texConfig.entries.Length;
            }

            var result = new NativeArray<TextureEntryParams>(
                Math.Max(totalEntries, 1), allocator);
            int cursor = 0;
            // ノイズオフセットのグローバルカウンタ（全バイオームのエントリに通し番号を振る）
            int globalNoiseIdx = 0;

            for (int i = 0; i < biomeTypes.Length; i++)
            {
                var texConfig = helper.GetTextureConfig(biomeTypes[i]);
                int entryCount = (texConfig?.entries != null) ? texConfig.entries.Length : 0;

                // biomeParamsにスライス情報を書き戻す
                var bp = biomeParams[i];
                bp.textureEntryBase = cursor;
                bp.textureEntryCount = entryCount;
                biomeParams[i] = bp;

                // 既存パイプラインと同じRNG消費順: バイオーム順にオフセットを生成
                int needed = Math.Max(entryCount * 4, 4);
                ConsumeOffsets(textureRng, needed);

                for (int e = 0; e < entryCount; e++)
                {
                    var entry = texConfig.entries[e];

                    // TerrainLayerのグローバルインデックスを取得（nullはビーチ=0にフォールバック）
                    int layerIdx = 0;
                    if (entry.layer != null && layerIndexMap.ContainsKey(entry.layer))
                        layerIdx = layerIndexMap[entry.layer];

                    result[cursor + e] = new TextureEntryParams
                    {
                        layerIndex = layerIdx,
                        weight = entry.weight,

                        useSlopeFilter = entry.useSlopeFilter ? 1 : 0,
                        slopeMin = entry.slopeMin,
                        slopeMax = entry.slopeMax,
                        slopeSmoothness = entry.slopeSmoothness,

                        useHeightFilter = entry.useHeightFilter ? 1 : 0,
                        heightMin = entry.heightMin,
                        heightMax = entry.heightMax,
                        heightSmoothness = entry.heightSmoothness,

                        useCurvatureFilter = entry.useCurvatureFilter ? 1 : 0,
                        curvatureMin = entry.curvatureMin,
                        curvatureMax = entry.curvatureMax,
                        curvatureSmoothness = entry.curvatureSmoothness,

                        noiseType = (int)entry.noiseType,
                        noiseFrequency = entry.noiseFrequency,
                        noiseAmplitude = entry.noiseAmplitude,
                        // テクスチャノイズ用オフセットはグローバル通し番号で参照
                        noiseOffsetIndex = globalNoiseIdx + e
                    };
                }

                cursor += entryCount;
                globalNoiseIdx += entryCount;
            }

            return result;
        }

        /// <summary>
        /// パイプラインで必要な全NativeArrayバッファを一括確保する。
        /// </summary>
        public static JobBuffers AllocateBuffers(
            int resolution, int biomeCount, int layerCount, Allocator allocator)
        {
            int pixelCount = resolution * resolution;
            int totalBiomeCols = biomeCount + 2;

            return new JobBuffers
            {
                shoreMask = new NativeArray<float>(pixelCount, allocator),
                landMask = new NativeArray<float>(pixelCount, allocator),
                beachFactor = new NativeArray<float>(pixelCount, allocator),
                coastalSmoothFactor = new NativeArray<float>(pixelCount, allocator),
                landTextureFactor = new NativeArray<float>(pixelCount, allocator),
                seaTextureFactor = new NativeArray<float>(pixelCount, allocator),
                rawBiomeIndex = new NativeArray<int>(pixelCount, allocator),
                rawBiomeWeights = new NativeArray<float>(pixelCount * totalBiomeCols, allocator),
                biomeWeights = new NativeArray<float>(pixelCount * totalBiomeCols, allocator),
                winnerBiomeIndex = new NativeArray<int>(pixelCount, allocator),
                heights = new NativeArray<float>(pixelCount, allocator),
                splatWeights = new NativeArray<float>(pixelCount * layerCount, allocator),
                blurTemp = new NativeArray<float>(pixelCount * totalBiomeCols, allocator),
                plateauMask = new NativeArray<float>(pixelCount, allocator),
                regionLabels = new NativeArray<int>(pixelCount, allocator),
                regionInfos = new NativeArray<PlateauRegionInfo>(64, allocator),
                regionCount = new NativeArray<int>(1, allocator),
            };
        }

        /// <summary>
        /// 全バイオームのTerrainLayerを収集し、重複排除したマネージド配列を返す。
        /// </summary>
        public static TerrainLayer[] BuildTerrainLayers(
            TerrainGenerationConfig config,
            BiomePlacementHelper helper,
            BiomeType[] biomeTypes,
            out Dictionary<TerrainLayer, int> layerIndexMap)
        {
            var layerList = new List<TerrainLayer>();
            layerIndexMap = new Dictionary<TerrainLayer, int>();

            // インデックス0はビーチ（Ocean/Beach共通）
            var shore = config.shoreConfig;
            if (shore?.beachLayer != null)
            {
                layerList.Add(shore.beachLayer);
                layerIndexMap[shore.beachLayer] = 0;
            }

            // 岩レイヤーがあれば追加（崖面テクスチャ用）
            if (config.rockLayer != null && !layerIndexMap.ContainsKey(config.rockLayer))
            {
                layerIndexMap[config.rockLayer] = layerList.Count;
                layerList.Add(config.rockLayer);
            }

            // 各バイオームのTerrainLayerとTextureConfigのレイヤーを登録
            for (int b = 0; b < biomeTypes.Length; b++)
            {
                var mainLayer = helper.GetTerrainLayer(biomeTypes[b]);
                if (mainLayer != null)
                    RegisterLayer(mainLayer, layerList, layerIndexMap);

                var texConfig = helper.GetTextureConfig(biomeTypes[b]);
                if (texConfig?.entries != null)
                {
                    for (int e = 0; e < texConfig.entries.Length; e++)
                    {
                        var layer = texConfig.entries[e].layer;
                        if (layer != null)
                            RegisterLayer(layer, layerList, layerIndexMap);
                    }
                }
            }

            return layerList.ToArray();
        }

        /// <summary>
        /// 単一バイオームのBiomeParamsを生成する。BiomeHeightmapExporter等の
        /// 診断ツールから呼ばれる。
        /// </summary>
        public static BiomeParams CreateSingleBiomeParams(
            TerrainGenerationConfig config, BiomeType type)
        {
            var bp = CreateDefaultParams((int)type);
            FillHeightParams(ref bp, config, type);
            return bp;
        }

        /// <summary>
        /// バイオームが必要とするノイズオフセット数をconfigから算出する。
        /// </summary>
        public static int GetNoiseOffsetCount(TerrainGenerationConfig config, BiomeType type)
        {
            switch (type)
            {
                case BiomeType.Grassland:
                    return 2;
                case BiomeType.Forest:
                    var f = config.forest;
                    // warp + baseOctaves + detailOctaves + ridgeOctaves
                    return f.warpIterations * 2 + f.baseOctaves + f.detailOctaves + f.ridgeOctaves;
                case BiomeType.Savanna:
                    return 7;
                case BiomeType.Desert:
                    return 3 + config.desert.canyonOctaves + config.desert.cliffOctaves;
                case BiomeType.Mesa:
                    var m = config.mesa;
                    // warp + 第1fBm + 第2fBm(孤立化) + canyon + ディテール予備
                    return m.warpIterations * 2 + m.octaves * 2 + m.canyonOctaves + 5;
                case BiomeType.Alpine:
                    var a = config.alpine;
                    // warp(iter*2) + radiusJitter(2) + mass(oct+1) + broadRidge(ridgeOct)
                    // + valley(4) + ridgeFine(4) + midFbm(4) + midRidge(4) + plateau(2) + broadVariation(2)
                    return a.warpIterations * 2 + (a.octaves + 1) + a.ridgeOctaves + 22;
                case BiomeType.Jungle:
                    // warp(oct*2) + voronoi(1) + surfaceDetail(2)
                    return config.jungle.warpOctaves * 2 + 3;
                case BiomeType.Woods:
                    return 4;
                default:
                    return 0;
            }
        }

        // --- 内部ヘルパー ---

        /// <summary>
        /// BiomeTypeとconfigから基本パラメータを構築する。
        /// </summary>
        static BiomeParams CreateBaseParams(BiomeType biomeType,
            TerrainGenerationConfig config, BiomePlacementHelper helper)
        {
            var bp = CreateDefaultParams((int)biomeType);
            bp.classifyPriority = GetClassifyPriority(biomeType);
            bp.splatmapLayerIndex = helper.GetSplatmapLayerIndex(biomeType);

            // 海岸設定は共通Configから充填し、全バイオームで同じ水際判定を使う
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

            // 境界設定を共通configから充填
            var boundary = helper.GetBoundaryConfig();
            bp.heightBlendFastPathThreshold = boundary.heightBlendFastPathThreshold;
            bp.heightBlendMinWeight = boundary.heightBlendMinWeight;
            bp.boundaryNoiseSmoothstepWidth = boundary.boundaryNoiseSmoothstepWidth;
            bp.boundaryNoiseMidWeight = boundary.boundaryNoiseMidWeight;
            bp.boundaryNoiseHighWeight = boundary.boundaryNoiseHighWeight;

            // プラトー設定はAlpine固有
            if (biomeType == BiomeType.Alpine)
            {
                bp.enablePlateau = config.alpine.enablePlateau ? 1 : 0;
                bp.plateauSearchBaseRadius = config.alpine.plateauSearchBaseRadius;
                bp.plateauBoundaryBlend = config.alpine.plateauBoundaryBlend;
            }

            return bp;
        }

        /// <summary>
        /// 共通デフォルト値を持つBiomeParamsを作る。
        /// </summary>
        static BiomeParams CreateDefaultParams(int biomeType)
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

        /// <summary>
        /// バイオームのClassifyPriorityを返す。旧IBiomeDefinition.ClassifyPriorityと同一値。
        /// </summary>
        static int GetClassifyPriority(BiomeType type)
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

        /// <summary>
        /// TerrainGenerationConfigからバイオーム固有のconfigフィールドを読み出し、
        /// BiomeParamsの高さ生成パラメータに充填する。
        /// </summary>
        static void FillHeightParams(ref BiomeParams bp, TerrainGenerationConfig config, BiomeType type)
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
            // ベースレイヤー（低周波の広域うねり）
            bp.frequency = c.baseFrequency;
            bp.octaves = c.baseOctaves;
            bp.persistence = c.basePersistence;
            bp.lacunarity = 2f;
            // ディテールレイヤー（高周波の表面テクスチャ）
            bp.secondaryFrequency = c.detailFrequency;
            bp.secondaryAmplitude = c.detailWeight;
            bp.canyonOctaves = c.detailOctaves;
            // ドメインワープ
            bp.domainWarpStrength = c.warpStrength;
            bp.domainWarpIterations = c.warpIterations;
            // プラトー平坦化
            bp.plateauFlatten = c.plateauFlatten;
            // リッジ
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
            // 台地位置ノイズの周波数
            bp.secondaryFrequency = c.plateauFrequency;
            // 平原起伏の振幅をexponentに流用
            bp.exponent = c.undulationAmplitude;
            // テラス段数をterraceStepsに流用
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
            // 積ノイズ孤立化 + 境界ノイズ + smoothstep閾値 + テラス + プラトー平坦化
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
            // 台地上ノイズ: ridgeBlend=強度、exponent=周波数倍率
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
            // 山頂平坦化: 天井は汎用フィールド、床は専用フィールドに格納
            bp.plateauFlatten = c.ceilStrength;
            bp.secondaryFrequency = c.ceilHeight;
            bp.floorHeight = c.floorHeight;
            bp.floorStrength = c.floorStrength;
        }

        static void FillJungle(ref BiomeParams bp, JungleBiomeConfig c)
        {
            bp.baseHeight = c.baseHeight;
            bp.hillAmplitude = c.amplitude;
            // Voronoiセルサイズとワープ
            bp.frequency = c.terraceFrequency;
            bp.octaves = c.warpOctaves;
            bp.terraceSteps = c.terraceStepCount;
            bp.domainWarpStrength = c.warpStrength;
            // セル固有の高さバリエーション
            bp.ridgeBlend = c.cellHeightVariation;
            // 境界スロープ（Voronoiエッジ上で周期的にスロープ/崖を交互配置）
            bp.absSmoothing = c.slopeWidth;
            bp.secondaryFrequency = c.slopeRepeat;
            bp.secondaryAmplitude = c.slopeCoverage;
            // 表面ディテール: 元座標FBmで段上面に±変位
            bp.plateauFlatten = c.surfaceDetailAmplitude;
            bp.exponent = c.surfaceDetailFrequency;
            // ガウシアンブラー半径（terraceSharpness経由でパイプラインに渡す）
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

        /// <summary>
        /// Classify条件をmin/max範囲に変換する。
        /// </summary>
        static void FillClassificationRange(
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

        static void ConsumeOffsets(System.Random rng, int count)
        {
            for (int i = 0; i < count; i++)
            {
                rng.NextDouble();
                rng.NextDouble();
            }
        }

        static int RegisterLayer(TerrainLayer layer,
            List<TerrainLayer> layerList, Dictionary<TerrainLayer, int> indexMap)
        {
            if (indexMap.TryGetValue(layer, out int existing))
                return existing;
            int idx = layerList.Count;
            layerList.Add(layer);
            indexMap[layer] = idx;
            return idx;
        }
    }
}
