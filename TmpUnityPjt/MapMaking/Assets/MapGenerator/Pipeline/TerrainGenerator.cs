using System.Threading.Tasks;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Generators;
using MapGenerator.Pipeline.Jobs;
using MapGenerator.Pipeline.Spawn;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapGenerator.Pipeline
{
    /// <summary>
    /// 5段パイプラインのオーケストレータ。
    /// ステージ1-2はDOTSジョブで並列実行し、ステージ3-5はマネージドコードで配置処理を行う。
    /// </summary>
    public static class TerrainGenerator
    {
        /// <summary>
        /// パディング付きで生成し、内側をクロップしてチャンク境界シームを解消する。
        /// padding > 0 のとき、resolution + 2*padding の拡張領域で生成し、中央の resolution 分を返す。
        /// </summary>
        public static TerrainGenerationResult GenerateWithPadding(TerrainGenerationConfig config)
        {
            int padding = config.chunkPadding;
            if (padding <= 0) return Generate(config);

            int baseRes = (int)config.resolutionPreset + 1;
            int paddedRes = baseRes + 2 * padding;
            float pixelSizeX = config.terrainWidth / (baseRes - 1);
            float pixelSizeZ = config.terrainLength / (baseRes - 1);

            // 元の値を退避（finally で確実に復元する）
            float origOffX = config.worldOffsetX;
            float origOffZ = config.worldOffsetZ;
            int origOverride = config.overrideResolution;
            float origWidth = config.terrainWidth;
            float origLength = config.terrainLength;
            bool origObject = config.generateObject;
            bool origDetail = config.generateDetail;
            bool origOre = config.generateOre;

            // Phase 1: パディング付きで高さ・テクスチャのみ生成（配置はスキップ）
            config.worldOffsetX -= padding * pixelSizeX;
            config.worldOffsetZ -= padding * pixelSizeZ;
            config.overrideResolution = paddedRes;
            config.terrainWidth = pixelSizeX * (paddedRes - 1);
            config.terrainLength = pixelSizeZ * (paddedRes - 1);
            config.generateObject = false;
            config.generateDetail = false;
            config.generateOre = false;

            TerrainGenerationResult paddedResult;
            try
            {
                paddedResult = Generate(config);
            }
            finally
            {
                config.worldOffsetX = origOffX;
                config.worldOffsetZ = origOffZ;
                config.overrideResolution = origOverride;
                config.terrainWidth = origWidth;
                config.terrainLength = origLength;
                config.generateObject = origObject;
                config.generateDetail = origDetail;
                config.generateOre = origOre;
            }

            // Phase 2: 高さ・テクスチャのみクロップ（配置データは空）
            var result = CropResult(paddedResult, paddedRes, baseRes, padding, origWidth, origLength);

            // Phase 3-4: オリジナル寸法で分類→配置実行
            // 鉱脈はRunPlacementStages内で生成されるため、generateOreもゲートに含める
            bool needPlacement = (origObject || origDetail || origOre);
            if (needPlacement)
            {
                var helper = new BiomePlacementHelper(config);
                var biomeTypes = GetEnabledBiomeTypes(config);
                if (HasAnyPlacement(config, helper, biomeTypes))
                {
                    // パディングなしのオリジナル寸法でバイオーム分類を実行
                    var biomeWeights2D = RunClassificationForPlacement(config, biomeTypes);
                    // クロップ済み高さ＋オリジナル分類データで配置
                    RunPlacementStages(config, helper, biomeTypes,
                        result.Heights, baseRes, result, biomeWeights2D);
                }
            }

            return result;
        }

        /// <summary>
        /// パディング付き生成結果から内側のbaseRes分だけを切り出す。
        /// Heights・Splatmapのみクロップ。配置データはRunPlacementStagesが別途設定する。
        /// </summary>
        static TerrainGenerationResult CropResult(
            TerrainGenerationResult padded, int paddedRes, int baseRes, int padding,
            float terrainWidth, float terrainLength)
        {
            int basePixels = baseRes * baseRes;
            var croppedHeights = new float[basePixels];
            for (int y = 0; y < baseRes; y++)
                for (int x = 0; x < baseRes; x++)
                    croppedHeights[y * baseRes + x] = padded.Heights[(y + padding) * paddedRes + (x + padding)];

            var result = new TerrainGenerationResult
            {
                Heights = croppedHeights,
                Resolution = baseRes,
                TerrainSize = new Vector3(terrainWidth, padded.TerrainSize.y, terrainLength),
                TerrainLayers = padded.TerrainLayers,
            };

            if (padded.Splatmap != null)
            {
                int paddedARes = paddedRes - 1;
                int baseARes = baseRes - 1;
                int layers = padded.Splatmap.GetLength(2);
                var cropped = new float[baseARes, baseARes, layers];
                for (int y = 0; y < baseARes; y++)
                    for (int x = 0; x < baseARes; x++)
                        for (int l = 0; l < layers; l++)
                            cropped[y, x, l] = padded.Splatmap[y + padding, x + padding, l];
                result.Splatmap = cropped;
            }

            return result;
        }

        public static TerrainGenerationResult Generate(TerrainGenerationConfig config)
        {
            PipelineProfiler.Begin();

            // BiomePlacementHelperでステージ3-5の配置ロジックを提供
            var helper = new BiomePlacementHelper(config);
            var biomeTypes = GetEnabledBiomeTypes(config);
            int biomeCount = biomeTypes.Length;
            int res = config.Resolution;
            int pixelCount = res * res;

            // テクスチャレイヤーを収集し、重複排除したグローバルインデックスを構築
            var terrainLayers = JobDataConverter.BuildTerrainLayers(
                config, helper, biomeTypes, out var layerIndexMap);

            // デバッグ用レイヤーをtextureConfigとは独立に末尾追加（SplatmapJobは無関知）
            int debugLayerStart = terrainLayers.Length;
            if (config.alpine.debugPlateauOverlay && config.alpine.debugTerrainLayers != null
                && config.alpine.debugTerrainLayers.Length > 0)
            {
                var extended = new System.Collections.Generic.List<TerrainLayer>(terrainLayers);
                foreach (var dl in config.alpine.debugTerrainLayers)
                    if (dl != null) extended.Add(dl);
                terrainLayers = extended.ToArray();
            }
            int layerCount = terrainLayers.Length;

            // BiomeParams/NoiseOffsets/TextureEntriesのNativeArray変換
            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);

            // splatmapLayerIndexをlayerIndexMapの実インデックスに修正。
            // BiomePlacementHelper.GetSplatmapLayerIndex()のハードコード値(1-8)は
            // 有効バイオーム数によって実際のレイヤー配列と不一致になるため上書きする
            for (int i = 0; i < biomeTypes.Length; i++)
            {
                var mainLayer = helper.GetTerrainLayer(biomeTypes[i]);
                var bp = biomeParams[i];
                bp.splatmapLayerIndex = (mainLayer != null && layerIndexMap.ContainsKey(mainLayer))
                    ? layerIndexMap[mainLayer] : 0;
                biomeParams[i] = bp;
            }
            var noiseOffsets = JobDataConverter.GenerateNoiseOffsets(
                config, biomeParams, biomeTypes, Allocator.TempJob);
            var textureEntries = JobDataConverter.ConvertTextureEntries(
                config, biomeParams, helper, biomeTypes, layerIndexMap, Allocator.TempJob);

            // 分類用ノイズオフセット（大陸性/浸食の2種。バイオーム配置はボロノイで決定）
            JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob,
                out var continentalnessOffsets, out var erosionOffsets);

            // layerCount=0（テクスチャ未設定）だとSplatmapJobのバッファが空でクラッシュするため最低1を確保
            int effectiveLayerCount = System.Math.Max(layerCount, 1);
            var buffers = JobDataConverter.AllocateBuffers(res, biomeCount, effectiveLayerCount, Allocator.TempJob);

            // ReadOnlyデータをバッファに紐付け（Disposeで一括解放される）
            buffers.noiseOffsets = noiseOffsets;
            buffers.biomeParams = biomeParams;
            buffers.textureEntries = textureEntries;

            try
            {
                PipelineProfiler.Lap("データ準備（Helper/Layer/NativeArray変換）");

                // --- ステージ1-2: DOTSジョブパイプライン(ハイトマップ+スプラットマップ+台地検出) ---
                RunJobPipeline(config, biomeCount, effectiveLayerCount, debugLayerStart, buffers,
                    continentalnessOffsets, erosionOffsets);

                // ジョブ結果をマネージド配列に変換
                PipelineProfiler.Lap("ジョブ結果→マネージド変換");
                var heights = new float[pixelCount];
                buffers.heights.CopyTo(heights);

                var result = new TerrainGenerationResult
                {
                    Heights = heights,
                    Resolution = res,
                    TerrainSize = new Vector3(config.terrainWidth, config.terrainHeight, config.terrainLength),
                };

                // generateHeightmap=false なら高さデータを結果に含めない（テレインへの適用をスキップ）
                if (!config.generateHeightmap)
                    result.Heights = null;

                // スプラットマップをNativeArray→float[,,]に変換
                if (config.generateTexture && config.shoreConfig?.beachLayer != null && layerCount > 0)
                {
                    int aRes = config.AlphamapResolution;
                    result.Splatmap = TerrainApplier.ConvertSplatWeights(
                        buffers.splatWeights, res, aRes, layerCount);
                    result.TerrainLayers = terrainLayers;
                }

                PipelineProfiler.Lap("Splatmap変換");

                // ステージ3-5: per-biome mask方式で生成
                // 鉱脈はRunPlacementStages内で生成されるため、generateOreもゲートに含める
                bool needPlacement = (config.generateObject || config.generateDetail || config.generateOre)
                    && HasAnyPlacement(config, helper, biomeTypes);
                if (needPlacement)
                {
                    int totalBiomeCols = biomeCount + 2;
                    var biomeWeights2D = ConvertBiomeWeightsForPlacement(
                        buffers.biomeWeights, buffers.shoreMask, buffers.landMask, buffers.beachFactor,
                        res, biomeCount, totalBiomeCols);
                    PipelineProfiler.Lap("BiomeWeights変換");
                    RunPlacementStages(config, helper, biomeTypes, heights, res, result, biomeWeights2D);
                }

                return result;
            }
            finally
            {
                // NativeArrayの確実な解放
                buffers.Dispose();
                if (continentalnessOffsets.IsCreated) continentalnessOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
            }
        }

        /// <summary>
        /// 分類+高さ+テクスチャの全ジョブを順に実行する（従来のRunJobPipelineと同一動作）。
        /// </summary>
        static void RunJobPipeline(
            TerrainGenerationConfig config,
            int biomeCount, int layerCount, int debugLayerStart,
            JobBuffers buffers,
            NativeArray<Unity.Mathematics.float2> continentalnessOffsets,
            NativeArray<Unity.Mathematics.float2> erosionOffsets)
        {
            RunClassificationPipeline(config, biomeCount, buffers, continentalnessOffsets, erosionOffsets);
            RunHeightSplatPipeline(config, biomeCount, layerCount, debugLayerStart, buffers);
        }

        /// <summary>
        /// バイオーム分類パイプライン（Jobs 1a〜1c-V）。
        /// ボロノイ分類→補間→ブラーでバイオーム重みを確定する。高さ・テクスチャには依存しない。
        /// </summary>
        static void RunClassificationPipeline(
            TerrainGenerationConfig config,
            int biomeCount,
            JobBuffers buffers,
            NativeArray<Unity.Mathematics.float2> continentalnessOffsets,
            NativeArray<Unity.Mathematics.float2> erosionOffsets,
            bool protectEdgeSea = false)
        {
            int res = config.Resolution;
            int pixelCount = res * res;
            var shoreConfig = config.shoreConfig ?? new BiomeShoreConfig();

            // ボロノイ四色定理配色用の並び替えテーブル
            var biomePermutation = JobDataConverter.GenerateBiomePermutation(
                config.seed, biomeCount, Allocator.TempJob);

            // Job 1a: Continentalness+Erosionで陸/海判定 + ボロノイでバイオーム分類
            PipelineProfiler.Lap("分類パイプライン準備");
            var classJob = new ClassificationJob
            {
                resolution = res,
                terrainWidth = config.terrainWidth,
                terrainLength = config.terrainLength,
                worldOffsetX = config.worldOffsetX,
                worldOffsetZ = config.worldOffsetZ,
                continentalnessFrequency = config.continentalnessFrequency,
                continentalnessOctaves = config.continentalnessOctaves,
                continentalnessPersistence = config.continentalnessPersistence,
                landThreshold = config.landThreshold,
                erosionFrequency = config.erosionFrequency,
                erosionOctaves = config.erosionOctaves,
                erosionStrength = config.erosionStrength,
                beachWidth = 0f, // 2値化のみ。ビーチ遷移はBeachTransitionJobで生成
                voronoiCellSize = config.voronoiCellSize,
                voronoiJitter = config.voronoiJitter,
                biomeCount = biomeCount,
                seed = config.seed,
                boundaryWarpOctaves = config.boundaryWarpOctaves,
                boundaryWarpStrength = config.boundaryWarpStrength,
                boundaryWarpFrequency = config.boundaryWarpFrequency,
                continentalnessOffsets = continentalnessOffsets,
                erosionOffsets = erosionOffsets,
                biomePermutation = biomePermutation,
                shoreMask = buffers.shoreMask,
                landMask = buffers.landMask,
                beachFactor = buffers.beachFactor,
                rawBiomeIndex = buffers.rawBiomeIndex
            };
            classJob.Schedule(pixelCount, 64).Complete();
            biomePermutation.Dispose();
            PipelineProfiler.Lap("Job 1a: Classification");

            // Job 1a-post: 小さな海領域を陸に変換（ビーチ判定前に実行）
            if (shoreConfig.minSeaRegionSize > 0)
            {
                var seaRemoval = new SmallSeaRemovalJob
                {
                    resolution = res,
                    minRegionSize = shoreConfig.minSeaRegionSize,
                    protectEdgeRegions = protectEdgeSea,
                    shoreMask = buffers.shoreMask,
                    landMask = buffers.landMask,
                    rawBiomeIndex = buffers.rawBiomeIndex
                };
                seaRemoval.Schedule().Complete();
            }

            PipelineProfiler.Lap("Job 1a-post: SmallSeaRemoval");

            // Job 1a-beach: EDT距離場＋Burstジョブでビーチ遷移帯を生成
            // 旧実装: per-pixel O(r²) のブルートフォース → 新実装: O(n) EDT + O(1) Burst lookup
            {
                // landMask → マネージド配列にコピーしてEDT実行
                var landArr = new float[pixelCount];
                buffers.landMask.CopyTo(landArr);

                // EDT で2乗ピクセル距離場を計算
                var distToSeaSqArr = ComputeDistanceFieldSq(landArr, res, false); // 海をシード
                var distToLandSqArr = ComputeDistanceFieldSq(landArr, res, true); // 陸をシード

                // NativeArray にコピーして Burst ジョブに渡す
                var distToSeaSq = new NativeArray<float>(pixelCount, Allocator.TempJob);
                var distToLandSq = new NativeArray<float>(pixelCount, Allocator.TempJob);
                distToSeaSq.CopyFrom(distToSeaSqArr);
                distToLandSq.CopyFrom(distToLandSqArr);

                var beachJob = new BeachTransitionJob
                {
                    resolution = res,
                    beachLandTextureRadius = shoreConfig.beachLandTextureRadius,
                    beachLandTerrainRadius = shoreConfig.beachLandTerrainRadius,
                    beachSeaTextureRadius = shoreConfig.beachSeaTextureRadius,
                    beachSeaTerrainRadius = shoreConfig.beachSeaTerrainRadius,
                    landMask = buffers.landMask,
                    distToSeaSq = distToSeaSq,
                    distToLandSq = distToLandSq,
                    shoreMask = buffers.shoreMask,
                    beachFactor = buffers.beachFactor,
                    coastalSmoothFactor = buffers.coastalSmoothFactor,
                    landTextureFactor = buffers.landTextureFactor,
                    seaTextureFactor = buffers.seaTextureFactor
                };
                beachJob.Schedule(pixelCount, 64).Complete();

                distToSeaSq.Dispose();
                distToLandSq.Dispose();
            }
            PipelineProfiler.Lap("Job 1a-beach: BeachTransition");

            // Job 1b: MC式バイオーム補間（周囲サンプリングで距離ベース重みを算出）
            var interpJob = new InterpolateWeightsJob
            {
                resolution = res,
                biomeCount = biomeCount,
                blendRadius = config.biomeBlendRadius,
                rawBiomeIndex = buffers.rawBiomeIndex,
                biomeParams = buffers.biomeParams,
                rawBiomeWeights = buffers.rawBiomeWeights
            };
            interpJob.Schedule(pixelCount, 64).Complete();
            PipelineProfiler.Lap("Job 1b: InterpolateWeights");

            // Job 1c-H: 水平ボックスブラーでバイオーム重みを平滑化
            int divisor = Mathf.Max(1, config.boundaryConfig.blurRadiusDivisor);
            int blurRadius = config.biomeBlendRadius / divisor;
            var hBlur = new HorizontalBlurJob
            {
                resolution = res,
                biomeCount = biomeCount,
                blurRadius = blurRadius,
                rawBiomeWeights = buffers.rawBiomeWeights,
                rawBiomeIndex = buffers.rawBiomeIndex,
                blurTemp = buffers.blurTemp
            };
            hBlur.Schedule(res, 1).Complete();

            // Job 1c-V: 垂直ボックスブラーで最終バイオーム重みとwinner算出
            var vBlur = new VerticalBlurJob
            {
                resolution = res,
                biomeCount = biomeCount,
                blurRadius = blurRadius,
                blurTemp = buffers.blurTemp,
                rawBiomeIndex = buffers.rawBiomeIndex,
                biomeWeights = buffers.biomeWeights,
                winnerBiomeIndex = buffers.winnerBiomeIndex
            };
            vBlur.Schedule(res, 1).Complete();
            PipelineProfiler.Lap("Job 1c: BiomeBlur (H+V)");
        }

        /// <summary>段2: 局所窓の本番一致分類結果（post-blur）。</summary>
        internal sealed class WindowClassification
        {
            public int Resolution;        // 窓のサンプル解像度(px)
            public float ActualWindowSize; // 実窓サイズ(m) = (Resolution-1)*pitch
            public float OriginX, OriginZ; // 窓原点ワールド座標(m)
            public float PitchX, PitchZ;   // m/px（本番一致）
            public int[] WinnerBiomeIndex;  // 有効バイオーム配列インデックス
            public float[] LandMask;        // 1=陸,0=海
            public float[] BeachFactor;     // 0-1
        }

        /// <summary>
        /// 段2: 本番m/pxに一致する局所窓で、SmallSeaRemoval(保護)+Interpolate+Blurまで実行し
        /// final winner / land / beach を返す。
        /// 前提1: baseConfig は本番設定（overrideResolution==0）であること。ピッチは本番Resolution基準で算出する。
        /// 前提2: 窓winnerが本番winnerとビット一致するのは pitch = terrainWidth/(Resolution-1) が float で
        /// 正確表現できる場合（既定の 2^n+1 プリセット×terrainWidth=1000 はゼロ誤差）。非可表現ピッチでは
        /// 境界隣接画素が最大数cm相当でずれうるが、内部点選定（距離変換/pole）には影響しない。
        /// </summary>
        internal static WindowClassification RunClassificationDetailed(
            TerrainGenerationConfig baseConfig, BiomeType[] biomeTypes,
            float windowCenterX, float windowCenterZ, float windowSize)
        {
            if (baseConfig.overrideResolution != 0)
                throw new System.ArgumentException(
                    "RunClassificationDetailed requires the production config (overrideResolution == 0); " +
                    "pitch/prediction-match depends on production Resolution.", nameof(baseConfig));

            // 本番m/px（X/Z別管理）
            double pX = (double)baseConfig.terrainWidth / (baseConfig.Resolution - 1);
            double pZ = (double)baseConfig.terrainLength / (baseConfig.Resolution - 1);
            // 正方窓: pitchはX基準で解像度を決め、actualでX/Zそろえる（terrainWidth==terrainLength前提、異なる場合はpを別々に）
            int res = Mathf.CeilToInt((float)(windowSize / pX)) + 1;
            res = Mathf.Max(2, res);
            double actualX = (res - 1) * pX;
            double actualZ = (res - 1) * pZ;

            // 窓原点を本番サンプル格子にスナップ（ワールド原点基準で pX/pZ 刻み）
            double rawOriginX = windowCenterX - actualX * 0.5;
            double rawOriginZ = windowCenterZ - actualZ * 0.5;
            float originX = (float)(System.Math.Round(rawOriginX / pX) * pX);
            float originZ = (float)(System.Math.Round(rawOriginZ / pZ) * pZ);

            int biomeCount = biomeTypes.Length;
            int pixelCount = res * res;

            // cfg複製と全NativeArray確保をtry内で行い、途中throw時もfinallyで確実に解放する。
            TerrainGenerationConfig cfg = null;
            NativeArray<BiomeParams> biomeParams = default;
            NativeArray<float2> contOffsets = default, erosionOffsets = default;
            JobBuffers buffers = default;
            bool buffersAllocated = false;
            try
            {
                // 窓専用configを複製（SOを汚さない）し、overrideResolutionで窓解像度を指定
                cfg = Object.Instantiate(baseConfig);
                cfg.overrideResolution = res;
                cfg.terrainWidth = (float)actualX;
                cfg.terrainLength = (float)actualZ;
                cfg.worldOffsetX = originX;
                cfg.worldOffsetZ = originZ;

                biomeParams = JobDataConverter.ConvertBiomeParams(cfg, biomeTypes, Allocator.TempJob);
                JobDataConverter.GenerateClassificationOffsets(cfg, Allocator.TempJob,
                    out contOffsets, out erosionOffsets);
                buffers = JobDataConverter.AllocateBuffers(res, biomeCount, 1, Allocator.TempJob);
                buffers.biomeParams = biomeParams;
                buffersAllocated = true;

                // 段2は窓端の大海を保護してクリップ誤判定を防ぐ
                RunClassificationPipeline(cfg, biomeCount, buffers, contOffsets, erosionOffsets, protectEdgeSea: true);

                var winner = new int[pixelCount];
                var land = new float[pixelCount];
                var beach = new float[pixelCount];
                buffers.winnerBiomeIndex.CopyTo(winner);
                buffers.landMask.CopyTo(land);
                buffers.beachFactor.CopyTo(beach);

                return new WindowClassification
                {
                    Resolution = res,
                    ActualWindowSize = (float)actualX,
                    OriginX = originX,
                    OriginZ = originZ,
                    PitchX = (float)pX,
                    PitchZ = (float)pZ,
                    WinnerBiomeIndex = winner,
                    LandMask = land,
                    BeachFactor = beach
                };
            }
            finally
            {
                // buffersAllocated 時は buffers.biomeParams = biomeParams なので buffers.Dispose() が解放する。
                // 未割当時（AllocateBuffers前にthrow）のみ biomeParams を個別解放（二重Dispose回避）。
                if (buffersAllocated) buffers.Dispose();
                else if (biomeParams.IsCreated) biomeParams.Dispose();
                if (contOffsets.IsCreated) contOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
                if (cfg != null) Object.DestroyImmediate(cfg);
            }
        }

        /// <summary>
        /// 高さ・テクスチャ生成パイプライン（Jobs 2a〜2g）。
        /// 分類パイプラインの出力（biomeWeights, winnerBiomeIndex等）に依存する。
        /// </summary>
        static void RunHeightSplatPipeline(
            TerrainGenerationConfig config,
            int biomeCount, int layerCount, int debugLayerStart,
            JobBuffers buffers)
        {
            int res = config.Resolution;
            int pixelCount = res * res;
            var shoreConfig = config.shoreConfig ?? new BiomeShoreConfig();

            // Job 2a: バイオーム加重ブレンドでピクセルごとの高さを生成
            var heightJob = new HeightSampleJob
            {
                resolution = res,
                biomeCount = biomeCount,
                terrainWidth = config.terrainWidth,
                terrainLength = config.terrainLength,
                worldOffsetX = config.worldOffsetX,
                worldOffsetZ = config.worldOffsetZ,
                seaLevel = config.seaLevel,
                beachElevation = shoreConfig.beachElevation,
                shoreMask = buffers.shoreMask,
                landMask = buffers.landMask,
                beachFactor = buffers.beachFactor,
                biomeWeights = buffers.biomeWeights,
                winnerBiomeIndex = buffers.winnerBiomeIndex,
                biomeParams = buffers.biomeParams,
                noiseOffsets = buffers.noiseOffsets,
                heights = buffers.heights
            };
            heightJob.Schedule(pixelCount, 64).Complete();
            PipelineProfiler.Lap("Job 2a: HeightSample");

            // 砂浜近傍だけを1回平均化して、浜と内陸の継ぎ目を単純に和らげる
            var coastalSmooth = new CoastalSmoothJob
            {
                resolution = res,
                landMask = buffers.landMask,
                coastalSmoothFactor = buffers.coastalSmoothFactor,
                inputHeights = buffers.heights,
                outputHeights = buffers.blurTemp.GetSubArray(0, pixelCount)
            };
            coastalSmooth.Schedule(pixelCount, 64).Complete();
            NativeArray<float>.Copy(buffers.blurTemp, 0, buffers.heights, 0, pixelCount);
            PipelineProfiler.Lap("Job 2a-coast: CoastalSmooth");

            // Job 2b-2d: Alpine台地化パイプライン
            // 検出→領域分析→フラット化を heightmap 上で行い、その後 splatmap を生成する
            if (config.alpineEnabled && config.alpine.enablePlateau)
            {
                // 2b: prominence検出（per-pixel）
                var plateauDetect = new AlpinePlateauDetectionJob
                {
                    resolution = res,
                    prominenceThreshold = config.alpine.prominenceThreshold,
                    minProminentDirections = config.alpine.minProminentDirections,
                    heights = buffers.heights,
                    winnerBiomeIndex = buffers.winnerBiomeIndex,
                    biomeParams = buffers.biomeParams,
                    plateauMask = buffers.plateauMask
                };
                plateauDetect.Schedule(pixelCount, 64).Complete();

                // 2c: 連結領域分析（per-region判定 → regionLabels + regionInfos）
                var regionAnalysis = new PlateauRegionAnalysisJob
                {
                    resolution = res,
                    minRegionSize = config.alpine.minRegionSize,
                    minCoverageRatio = config.alpine.minPlateauCoverage,
                    coverageTolerance = config.alpine.coverageTolerance,
                    plateauMask = buffers.plateauMask,
                    heights = buffers.heights,
                    regionLabels = buffers.regionLabels,
                    regionInfos = buffers.regionInfos,
                    regionCount = buffers.regionCount
                };
                regionAnalysis.Schedule().Complete();

                // 診断ログ
                int nRegions = buffers.regionCount[0];
                Debug.Log($"[Plateau] regions={nRegions} minSize={config.alpine.minRegionSize} " +
                          $"minCov={config.alpine.minPlateauCoverage:F2} tol={config.alpine.coverageTolerance:F3}");
                for (int r = 0; r < nRegions; r++)
                {
                    var info = buffers.regionInfos[r];
                    Debug.Log($"[Plateau]   #{r + 1}: target={info.targetHeight:F4} " +
                              $"pixels={info.pixelCount} boundary={info.boundaryCount}");
                }

                // 2d: 領域メタデータを使ってフラット化（per-pixel計算、per-region目標高度）
                if (nRegions > 0)
                {
                    // 事後検証用に台地化前の高さをバックアップ
                    var heightsBackup = new NativeArray<float>(pixelCount, Allocator.TempJob);
                    NativeArray<float>.Copy(buffers.heights, heightsBackup);

                    var flatten = new PlateauFlattenJob
                    {
                        resolution = res,
                        baseTransition = config.alpine.plateauBaseTransition,
                        transitionScale = config.alpine.plateauTransitionScale,
                        boundaryBlend = config.alpine.plateauBoundaryBlend,
                        regionLabels = buffers.regionLabels,
                        regionInfos = buffers.regionInfos,
                        plateauMask = buffers.plateauMask,
                        heights = buffers.heights
                    };
                    flatten.Schedule(pixelCount, 64).Complete();

                    // 2e: 台地化後のカバー率を事後検証し、基準未達の領域をロールバック
                    var postValidation = new PlateauPostValidationJob
                    {
                        resolution = res,
                        minCoverageRatio = config.alpine.minPlateauCoverage,
                        coverageTolerance = config.alpine.coverageTolerance,
                        regionInfos = buffers.regionInfos,
                        regionCount = buffers.regionCount,
                        heightsBackup = heightsBackup,
                        regionLabels = buffers.regionLabels,
                        heights = buffers.heights
                    };
                    postValidation.Schedule().Complete();

                    heightsBackup.Dispose();

                    // 2d-post-2: 台地内部のスパイク除去（同一領域ボックスブラー）
                    if (config.alpine.smoothRadius > 0 && config.alpine.smoothIterations > 0)
                    {
                        var tempHeights = new NativeArray<float>(pixelCount, Allocator.TempJob);

                        for (int iter = 0; iter < config.alpine.smoothIterations; iter++)
                        {
                            NativeArray<float>.Copy(buffers.heights, tempHeights);
                            var smooth = new PlateauBoundarySmoothJob
                            {
                                resolution = res,
                                kernelRadius = Mathf.Min(config.alpine.smoothRadius, 4),
                                spikeThreshold = 0.004f,
                                regionLabels = buffers.regionLabels,
                                inputHeights = tempHeights,
                                outputHeights = buffers.heights
                            };
                            smooth.Schedule(pixelCount, 64).Complete();
                        }

                        tempHeights.Dispose();
                    }

                    // 2d-post-3: 境界帯ガウシアン＋パーリンノイズ（反復で段差を除去）
                    // ノイズは最終イテレーションのみ。先行回はガウシアンのみでリムを均す
                    {
                        var rng = new System.Random(config.seed + 99999);
                        var noiseOff = new float2(
                            (float)rng.NextDouble() * 10000f,
                            (float)rng.NextDouble() * 10000f);

                        int refineIter = Mathf.Max(config.alpine.boundaryRefineIterations, 1);
                        var refineInput = new NativeArray<float>(pixelCount, Allocator.TempJob);

                        for (int iter = 0; iter < refineIter; iter++)
                        {
                            NativeArray<float>.Copy(buffers.heights, refineInput);
                            bool isLastIter = iter == refineIter - 1;

                            var refine = new PlateauBoundaryRefineJob
                            {
                                resolution = res,
                                innerBand = config.alpine.boundaryInnerBand,
                                outerBand = config.alpine.boundaryOuterBand,
                                gaussSigma = config.alpine.boundaryGaussSigma,
                                noiseFrequency = config.alpine.boundaryNoiseFrequency,
                                noiseAmplitude = isLastIter ? config.alpine.boundaryNoiseAmplitude : 0f,
                                noiseOctaves = config.alpine.boundaryNoiseOctaves,
                                noiseOffset = noiseOff,
                                regionLabels = buffers.regionLabels,
                                regionInfos = buffers.regionInfos,
                                inputHeights = refineInput,
                                outputHeights = buffers.heights
                            };
                            refine.Schedule(pixelCount, 64).Complete();
                        }

                        refineInput.Dispose();
                    }
                }
            }

            PipelineProfiler.Lap("Job 2b-2e: Alpine Plateau");

            // Job 2a-blur: 高さマップのガウシアンブラー（Jungle段差平滑化用）
            int heightBlurRadius = GetHeightBlurRadius(config, buffers.biomeParams);
            if (heightBlurRadius > 0)
            {
                var hBlurH = new HeightBlurHorizontalJob
                {
                    resolution = res,
                    blurRadius = heightBlurRadius,
                    heights = buffers.heights,
                    blurTemp = buffers.blurTemp
                };
                hBlurH.Schedule(res, 1).Complete();

                var hBlurV = new HeightBlurVerticalJob
                {
                    resolution = res,
                    blurRadius = heightBlurRadius,
                    blurTemp = buffers.blurTemp,
                    heights = buffers.heights
                };
                hBlurV.Schedule(res, 1).Complete();
            }

            PipelineProfiler.Lap("Job 2a-blur: HeightBlur");

            // Job 2a-slope: ランダム地点を中心に追加平滑化（スロープ生成）
            var slopeInfo = GetSlopeParams(buffers.biomeParams);
            if (slopeInfo.density > 0f && slopeInfo.radius > 0 && slopeInfo.blendStrength > 0f)
            {
                NativeArray<float>.Copy(buffers.heights, 0, buffers.blurTemp, 0, pixelCount);

                var slopeJob = new HeightSlopeJob
                {
                    resolution = res,
                    slopeRadius = slopeInfo.radius,
                    slopeDensity = slopeInfo.density,
                    slopeCellSize = slopeInfo.cellSize,
                    slopeBlendStrength = slopeInfo.blendStrength,
                    terrainWidth = config.terrainWidth,
                    terrainLength = config.terrainLength,
                    blurTemp = buffers.blurTemp,
                    heights = buffers.heights
                };
                slopeJob.Schedule(res, 1).Complete();
            }

            PipelineProfiler.Lap("Job 2a-slope: HeightSlope");

            // Job 2a-noise: ブラー後の崖面を侵食ノイズで削る
            if (config.jungleEnabled && config.jungle.boundaryNoiseStrength > 0f)
            {
                NativeArray<float>.Copy(buffers.heights, 0, buffers.blurTemp, 0, pixelCount);

                var noiseJob = new BoundaryNoiseJob
                {
                    resolution = res,
                    terrainWidth = config.terrainWidth,
                    terrainLength = config.terrainLength,
                    terrainHeight = config.terrainHeight,
                    noiseStrength = config.jungle.boundaryNoiseStrength,
                    slopeThreshold = config.jungle.boundaryNoiseSlopeThreshold,
                    noiseFrequency = config.jungle.boundaryNoiseFrequency,
                    seed = 12345f,
                    smoothstepWidth = config.boundaryConfig.boundaryNoiseSmoothstepWidth,
                    noiseMidWeight = config.boundaryConfig.boundaryNoiseMidWeight,
                    noiseHighWeight = config.boundaryConfig.boundaryNoiseHighWeight,
                    readHeights = buffers.blurTemp,
                    heights = buffers.heights
                };
                noiseJob.Schedule(res, 1).Complete();
            }

            PipelineProfiler.Lap("Job 2a-noise: BoundaryNoise");

            // Job 2f: フラット化済みの高さからテクスチャ重みを決定（テクスチャ生成無効時はスキップ）
            if (config.generateTexture)
            {
                var splatJob = new SplatmapJob
                {
                    resolution = res,
                    biomeCount = biomeCount,
                    totalLayers = layerCount,
                    terrainWidth = config.terrainWidth,
                    terrainHeight = config.terrainHeight,
                    terrainLength = config.terrainLength,
                    worldOffsetX = config.worldOffsetX,
                    worldOffsetZ = config.worldOffsetZ,
                    textureBlendStrength = config.boundaryConfig.textureBlendStrength,
                    heights = buffers.heights,
                    shoreMask = buffers.shoreMask,
                    landMask = buffers.landMask,
                    beachFactor = buffers.beachFactor,
                    landTextureFactor = buffers.landTextureFactor,
                    seaTextureFactor = buffers.seaTextureFactor,
                    biomeWeights = buffers.biomeWeights,
                    winnerBiomeIndex = buffers.winnerBiomeIndex,
                    biomeParams = buffers.biomeParams,
                    noiseOffsets = buffers.noiseOffsets,
                    textureEntries = buffers.textureEntries,
                    splatWeights = buffers.splatWeights
                };
                splatJob.Schedule(pixelCount, 64).Complete();
            }

            PipelineProfiler.Lap("Job 2f: Splatmap");


            // Job 2g: デバッグオーバーレイ（受理済み台地領域を可視化）
            if (config.generateTexture && config.alpineEnabled && config.alpine.enablePlateau && config.alpine.debugPlateauOverlay)
            {
                // Alpine の base layer index を探す
                int alpineBaseLayer = 0;
                for (int b = 0; b < biomeCount; b++)
                {
                    if (buffers.biomeParams[b].biomeType == 7)
                    { alpineBaseLayer = buffers.biomeParams[b].splatmapLayerIndex; break; }
                }
                int dbgCount = config.alpine.debugTerrainLayers != null
                    ? config.alpine.debugTerrainLayers.Length : 0;
                var debugOverlay = new PlateauDebugOverlayJob
                {
                    resolution = res,
                    totalLayers = layerCount,
                    baseLayerIndex = alpineBaseLayer,
                    debugLayerStart = debugLayerStart,
                    debugLayerCount = dbgCount,
                    fadeRadius = Mathf.Max(config.alpine.smoothRadius / 2, 3),
                    plateauMask = buffers.plateauMask,
                    regionLabels = buffers.regionLabels,
                    splatWeights = buffers.splatWeights
                };
                debugOverlay.Schedule(pixelCount, 64).Complete();
            }
        }

        /// <summary>
        /// ジョブ出力のbiomeWeights(コンテンツのみ)を旧形式(Ocean/Beach列付き)に変換する。
        /// ステージ3-5の配置処理が[idx, 2+b]でコンテンツバイオームを参照するため必要。
        /// </summary>
        static float[,] ConvertBiomeWeightsForPlacement(
            NativeArray<float> jobWeights, NativeArray<float> shoreMask,
            NativeArray<float> landMask,
            NativeArray<float> beachFactor, int res, int biomeCount, int totalCols)
        {
            int pixelCount = res * res;
            var result = new float[pixelCount, totalCols];
            int bc = biomeCount;
            int tc = totalCols;

            // ピクセル単位で並列化。各ピクセルは独立（NativeArrayは読み取り専用）
            Parallel.For(0, pixelCount, i =>
            {
                float shore = shoreMask[i];
                float beach = beachFactor[i];

                // Ocean列: shoreMaskがほぼ0の深海ピクセル
                result[i, 0] = shore < 0.005f ? 1f : 0f;
                // Beach列: ベル型beachFactorが閾値超過の砂浜帯
                result[i, 1] = beach > 0.2f ? beach : 0f;

                // コンテンツバイオーム列をジョブ出力からコピー
                float contentSum = 0f;
                for (int b = 0; b < bc; b++)
                {
                    float w = jobWeights[i * bc + b];
                    result[i, 2 + b] = w;
                    contentSum += w;
                }

                // Ocean/Beachが支配的な場合、コンテンツ重みを縮小して合計1にする
                float oceanBeach = result[i, 0] + result[i, 1];
                if (oceanBeach > 0f && contentSum > 0f)
                {
                    float scale = Mathf.Max(0f, 1f - oceanBeach);
                    for (int b = 0; b < bc; b++)
                        result[i, 2 + b] *= scale / contentSum;
                }
            });

            return result;
        }

        /// <summary>
        /// 全ピクセルの傾斜をBurst並列ジョブで計算する。
        /// </summary>
        static float[,] ComputeSlopes(float[] heights, TerrainGenerationConfig config, int res)
        {
            int pixelCount = res * res;
            var nativeHeights = new NativeArray<float>(heights, Allocator.TempJob);
            var nativeSlopes = new NativeArray<float>(pixelCount, Allocator.TempJob);

            var job = new SlopeComputeJob
            {
                resolution = res,
                terrainWidth = config.terrainWidth,
                terrainHeight = config.terrainHeight,
                terrainLength = config.terrainLength,
                heights = nativeHeights,
                slopes = nativeSlopes
            };
            job.Schedule(pixelCount, 64).Complete();

            var slopes = new float[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    slopes[z, x] = nativeSlopes[z * res + x];

            nativeSlopes.Dispose();
            nativeHeights.Dispose();
            return slopes;
        }

        /// <summary>
        /// バイオームパラメータからガウシアンブラー半径を取得する。
        /// Jungleのように後処理ブラーが必要なバイオームがあれば最大半径を返す。
        /// terraceSharpnessを0-1の値からピクセル半径に変換（最大20px）。
        /// </summary>
        static int GetHeightBlurRadius(TerrainGenerationConfig config,
            NativeArray<BiomeParams> biomeParams)
        {
            int maxRadius = 0;
            for (int i = 0; i < biomeParams.Length; i++)
            {
                var bp = biomeParams[i];
                // Jungle(8)のみ後処理ブラーを使用
                if (bp.biomeType == 8 && bp.terraceSharpness > 0f)
                {
                    int r = (int)(bp.terraceSharpness * 20f);
                    if (r > maxRadius) maxRadius = r;
                }
            }
            return maxRadius;
        }

        struct SlopeInfo
        {
            public float density;
            public float cellSize;
            public int radius;
            public float blendStrength;
        }

        /// <summary>
        /// HeightSlopeJob用パラメータを取得する。
        /// Jungleは境界スロープをSampleJungle内で処理するため、
        /// canyonOctaves（radius）が0→HeightSlopeJobは発火しない。
        /// </summary>
        static SlopeInfo GetSlopeParams(NativeArray<BiomeParams> biomeParams)
        {
            for (int i = 0; i < biomeParams.Length; i++)
            {
                var bp = biomeParams[i];
                if (bp.canyonOctaves > 0 && bp.secondaryAmplitude > 0f)
                {
                    return new SlopeInfo
                    {
                        density = bp.secondaryAmplitude,
                        cellSize = bp.secondaryFrequency,
                        radius = bp.canyonOctaves,
                        blendStrength = bp.absSmoothing
                    };
                }
            }
            return default;
        }

        /// <summary>
        /// GetEnabledBiomeTypes の外部公開ラッパ（InfiniteTerrainManager / Spawn探索 / テスト用）。
        /// </summary>
        internal static BiomeType[] GetEnabledBiomeTypesPublic(TerrainGenerationConfig config)
            => GetEnabledBiomeTypes(config);

        /// <summary>
        /// configの有効フラグからバイオームタイプ配列を構築する。
        /// BiomeRegistryのソート順（ClassifyPriority降順）を維持。
        /// </summary>
        static BiomeType[] GetEnabledBiomeTypes(TerrainGenerationConfig config)
        {
            var list = new System.Collections.Generic.List<BiomeType>();
            // ClassifyPriority降順で登録（高優先度が先に判定される）
            if (config.alpineEnabled)    list.Add(BiomeType.Alpine);
            if (config.mesaEnabled)      list.Add(BiomeType.Mesa);
            if (config.jungleEnabled)    list.Add(BiomeType.Jungle);
            if (config.desertEnabled)    list.Add(BiomeType.Desert);
            if (config.forestEnabled)    list.Add(BiomeType.Forest);
            if (config.woodsEnabled)     list.Add(BiomeType.Woods);
            if (config.savannaEnabled)   list.Add(BiomeType.Savanna);
            if (config.grasslandEnabled) list.Add(BiomeType.Grassland);
            // フォールバック保証: 何も有効でなければGrasslandを強制追加
            if (list.Count == 0) list.Add(BiomeType.Grassland);
            return list.ToArray();
        }

        /// <summary>
        /// 段1: 本番同一のClassificationJob(raw)を粗グリッドで1回実行し、
        /// セルごとの有効バイオーム配列インデックス(海=-1)を返す。SmallSeaRemoval/blurは行わない。
        /// </summary>
        internal static CoarseBiomeGrid ClassifyRawGrid(
            TerrainGenerationConfig config, BiomeType[] biomeTypes,
            float centerX, float centerZ, float extent, float cellSize)
        {
            int biomeCount = biomeTypes.Length;
            // res>=2 を保証（return の extent/(res-1) のゼロ除算回避）
            int res = Mathf.Max(2, Mathf.CeilToInt(extent / cellSize) + 1);
            float originX = centerX - extent * 0.5f;
            float originZ = centerZ - extent * 0.5f;
            int pixelCount = res * res;

            // 全NativeArray確保をtry内で行い、途中throw時もfinallyで確実に解放する。
            NativeArray<float2> contOffsets = default, erosionOffsets = default;
            NativeArray<int> biomePermutation = default;
            NativeArray<int> rawBiomeIndex = default;
            NativeArray<float> shoreMask = default, landMask = default, beachFactor = default;

            try
            {
                JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob,
                    out contOffsets, out erosionOffsets);
                biomePermutation = JobDataConverter.GenerateBiomePermutation(
                    config.seed, biomeCount, Allocator.TempJob);
                rawBiomeIndex = new NativeArray<int>(pixelCount, Allocator.TempJob);
                shoreMask = new NativeArray<float>(pixelCount, Allocator.TempJob);
                landMask = new NativeArray<float>(pixelCount, Allocator.TempJob);
                beachFactor = new NativeArray<float>(pixelCount, Allocator.TempJob);

                // このフィールド構成は RunClassificationPipeline の ClassificationJob 構築と完全一致させること（prediction==production の保証根拠）。本番側にノイズ/voronoi/warpパラメータを追加したらここも更新する。
                var classJob = new ClassificationJob
                {
                    resolution = res,
                    terrainWidth = extent,
                    terrainLength = extent,
                    worldOffsetX = originX,
                    worldOffsetZ = originZ,
                    continentalnessFrequency = config.continentalnessFrequency,
                    continentalnessOctaves = config.continentalnessOctaves,
                    continentalnessPersistence = config.continentalnessPersistence,
                    landThreshold = config.landThreshold,
                    erosionFrequency = config.erosionFrequency,
                    erosionOctaves = config.erosionOctaves,
                    erosionStrength = config.erosionStrength,
                    beachWidth = 0f, // 2値化のみ。ビーチ遷移は段1では不要
                    voronoiCellSize = config.voronoiCellSize,
                    voronoiJitter = config.voronoiJitter,
                    biomeCount = biomeCount,
                    seed = config.seed,
                    boundaryWarpOctaves = config.boundaryWarpOctaves,
                    boundaryWarpStrength = config.boundaryWarpStrength,
                    boundaryWarpFrequency = config.boundaryWarpFrequency,
                    continentalnessOffsets = contOffsets,
                    erosionOffsets = erosionOffsets,
                    biomePermutation = biomePermutation,
                    shoreMask = shoreMask,
                    landMask = landMask,
                    beachFactor = beachFactor,
                    rawBiomeIndex = rawBiomeIndex
                };
                classJob.Schedule(pixelCount, 64).Complete();

                var arr = new int[pixelCount];
                rawBiomeIndex.CopyTo(arr);
                return new CoarseBiomeGrid(
                    arr, res, res, extent / (res - 1), originX, originZ);
            }
            finally
            {
                // 部分初期化throw時に default 配列へ Dispose して例外を覆い隠さないよう IsCreated でガード
                if (contOffsets.IsCreated) contOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
                if (biomePermutation.IsCreated) biomePermutation.Dispose();
                if (rawBiomeIndex.IsCreated) rawBiomeIndex.Dispose();
                if (shoreMask.IsCreated) shoreMask.Dispose();
                if (landMask.IsCreated) landMask.Dispose();
                if (beachFactor.IsCreated) beachFactor.Dispose();
            }
        }

        /// <summary>
        /// オリジナル寸法でバイオーム分類のみ実行し、配置用のbiomeWeights2Dを返す。
        /// GenerateWithPadding()でパディング付き高さ生成とは独立に分類を実行するために使用。
        /// </summary>
        static float[,] RunClassificationForPlacement(
            TerrainGenerationConfig config, BiomeType[] biomeTypes)
        {
            int biomeCount = biomeTypes.Length;
            int res = config.Resolution;

            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob,
                out var contOffsets, out var erosionOffsets);

            // 分類に必要なバッファのみ使用（heights/splatWeightsも確保されるが未使用）
            int effectiveLayerCount = System.Math.Max(1, 1);
            var buffers = JobDataConverter.AllocateBuffers(res, biomeCount, effectiveLayerCount, Allocator.TempJob);
            buffers.biomeParams = biomeParams;

            try
            {
                RunClassificationPipeline(config, biomeCount, buffers, contOffsets, erosionOffsets);

                int totalBiomeCols = biomeCount + 2;
                return ConvertBiomeWeightsForPlacement(
                    buffers.biomeWeights, buffers.shoreMask, buffers.landMask, buffers.beachFactor,
                    res, biomeCount, totalBiomeCols);
            }
            finally
            {
                buffers.Dispose();
                if (contOffsets.IsCreated) contOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
            }
        }

        /// <summary>
        /// ステージ3-5: 樹木・オブジェクト・ディテールの配置。
        /// Generate()とGenerateWithPadding()の両方から呼ばれる。
        /// biomeWeights2Dはオリジナル寸法の分類結果を受け取る。
        /// </summary>
        static void RunPlacementStages(
            TerrainGenerationConfig config,
            BiomePlacementHelper helper,
            BiomeType[] biomeTypes,
            float[] heights, int res,
            TerrainGenerationResult result,
            float[,] biomeWeights2D)
        {
            int biomeCount = biomeTypes.Length;
            bool wantObject = config.generateObject;
            bool wantDetail = config.generateDetail;

            var masks = Generators.Util.BiomeMaskBuilder.BuildAllWinnerMasks(biomeWeights2D, res, biomeCount);
            var heights2D = TerrainApplier.ConvertHeights(heights, res);
            PipelineProfiler.Lap("Mask/Heights2D構築");

            // 全プロトタイプを収集（PlacementEntry→TreeInstance変換用）
            var allTreePrototypes = new System.Collections.Generic.List<TreePrototype>();
            for (int b = 0; b < biomeCount; b++)
                allTreePrototypes.AddRange(helper.GetTreePrototypes(biomeTypes[b]));
            var prefabToProtoIndex = new System.Collections.Generic.Dictionary<GameObject, int>();
            for (int i = 0; i < allTreePrototypes.Count; i++)
                if (allTreePrototypes[i].prefab != null && !prefabToProtoIndex.ContainsKey(allTreePrototypes[i].prefab))
                    prefabToProtoIndex[allTreePrototypes[i].prefab] = i;

            // ===== Stage 3: Trees (per-biome) =====
            // 内部でBurstジョブ(NativeArray TempJob)を使うため Parallel.For不可
            var allTreeEntries = new System.Collections.Generic.List<PlacementEntry>();
            for (int b = 0; b < biomeCount; b++)
            {
                var tp = helper.GetTreePlacementConfig(biomeTypes[b]);
                if (tp?.prototypes == null || tp.prototypes.Length == 0) continue;
                float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                var dims = TerrainDimensions.From(config, wm);
                var treeRng = new System.Random(config.seed + 3000 + b * 100);
                var entries = TreePlacementGenerator.GenerateForBiome(
                    masks[b], heights, dims, tp, treeRng);
                allTreeEntries.AddRange(entries);
            }
            PipelineProfiler.Lap("Stage 3: Tree配置");

            if (wantObject)
            {
                // ===== Stage 4: Objects (per-biome) =====
                var treeSpatialGrid = Generators.Util.SpatialGrid.FromPlacements(
                    allTreeEntries, config.terrainWidth, config.terrainLength);
                var allObjectEntries = new System.Collections.Generic.List<PlacementEntry>();
                for (int b = 0; b < biomeCount; b++)
                {
                    var oc = helper.GetObjectConfig(biomeTypes[b]);
                    if (oc == null) continue;
                    bool hasAny = (oc.entries?.Length > 0) || (oc.clusterEntries?.Length > 0);
                    if (!hasAny) continue;
                    float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                    var dims = TerrainDimensions.From(config, wm);
                    var objRng = new System.Random(config.seed + 4000 + b * 100);
                    var entries = ObjectPlacementGenerator.GenerateForBiome(
                        masks[b], heights2D, dims, oc, objRng, treeSpatialGrid);
                    allObjectEntries.AddRange(entries);
                }

                PipelineProfiler.Lap("Stage 4: Object配置");

                // ===== Stage 4.5: 岩周辺樹木 (per-biome) =====
                // Stage 3で構築したtreeSpatialGridを再利用し、岩周辺の木も距離チェック対象にする
                var rockTreeGrid = Generators.Util.SpatialGrid.FromPlacements(
                    allTreeEntries, config.terrainWidth, config.terrainLength, 3f);
                var objectPlacements = ConvertToObjectPlacements(allObjectEntries);
                for (int b = 0; b < biomeCount; b++)
                {
                    var tp = helper.GetTreePlacementConfig(biomeTypes[b]);
                    if (tp?.prototypes == null || tp.prototypes.Length == 0) continue;
                    float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                    var dims = TerrainDimensions.From(config, wm);
                    var rockRng = new System.Random(config.seed + 5000 + b * 100);
                    var rockEntries = TreePlacementGenerator.GenerateAroundObjects(
                        masks[b], heights, dims, tp, objectPlacements, rockRng, rockTreeGrid);
                    allTreeEntries.AddRange(rockEntries);
                }

                PipelineProfiler.Lap("Stage 4.5: 岩周辺Tree配置");

                // PlacementEntry → result への変換
                result.TreePrototypes = allTreePrototypes.ToArray();
                result.TreeInstances = ConvertToTreeInstances(
                    allTreeEntries, prefabToProtoIndex, config.terrainWidth,
                    config.terrainLength, config.terrainHeight);
                result.ObjectPlacements = objectPlacements;

                Debug.Log($"[MapGenerator] Generated {result.TreeInstances.Length} trees, " +
                          $"{result.ObjectPlacements.Count} objects.");

                // 樹木post-processing
                treeSpatialGrid = BuildTreeSpatialGrid(
                    result.TreeInstances, config.terrainWidth, config.terrainLength);
                var objectSpatialGrid = BuildObjectSpatialGrid(
                    result.ObjectPlacements, config.terrainWidth, config.terrainLength,
                    config.worldOffsetX, config.worldOffsetZ);

                // 岩周辺テクスチャ
                if (result.Splatmap != null && result.ObjectPlacements != null)
                {
                    ApplyObjectSurroundTexture(result.Splatmap, config, result.TerrainLayers,
                        result.ObjectPlacements, helper, biomeTypes, heights,
                        biomeWeights2D);
                }

                PipelineProfiler.Lap("Object周辺テクスチャ");

                // 樹木post-placement (height/texture modification)
                if (result.TreeInstances.Length > 0)
                {
                    var protoOffsets = TreePlacementGenerator.ComputePrototypeOffsets(helper, biomeTypes);
                    TreePlacementGenerator.ApplyHeightModification(
                        heights, res, config.terrainWidth, config.terrainHeight,
                        result.TreeInstances, helper, biomeTypes, protoOffsets);
                    if (result.Splatmap != null)
                        TreePlacementGenerator.ApplyTextureModification(
                            result.Splatmap, res, config.terrainWidth,
                            result.TerrainLayers, result.TreeInstances,
                            helper, biomeTypes, protoOffsets);
                }
                PipelineProfiler.Lap("Tree後処理（高さ/テクスチャ）");

                // ===== Stage 5: Details (per-biome) =====
                if (wantDetail)
                {
                    var slopes = ComputeSlopes(heights, config, res);
                    PipelineProfiler.Lap("Detail: Slope計算");

                    var allDetailProtos = new System.Collections.Generic.List<DetailPrototype>();
                    var allDetailMaps = new System.Collections.Generic.List<int[,]>();
                    for (int b = 0; b < biomeCount; b++)
                    {
                        var dc = helper.GetDetailConfig(biomeTypes[b]);
                        if (dc?.entries == null || dc.entries.Length == 0) continue;
                        float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                        var dims = TerrainDimensions.From(config, wm);
                        var detailRng = new System.Random(config.seed + 6000 + b * 100);

                        float treeMaxR = Generators.Util.SdfMapGenerator.ComputeMaxSearchRadius(dc.entries, true);
                        float objMaxR = Generators.Util.SdfMapGenerator.ComputeMaxSearchRadius(dc.entries, false);
                        var treeDistMap = treeMaxR > 0f ? Generators.Util.SdfMapGenerator.Generate(
                            treeSpatialGrid, config.AlphamapResolution, config.terrainWidth, config.terrainLength, treeMaxR) : null;
                        var objDistMap = objMaxR > 0f ? Generators.Util.SdfMapGenerator.Generate(
                            objectSpatialGrid, config.AlphamapResolution, config.terrainWidth, config.terrainLength, objMaxR) : null;
                        PipelineProfiler.Lap($"Detail: SDF距離マップ ({biomeTypes[b]})");

                        var (protos, maps) = DetailPlacementGenerator.GenerateForBiome(
                            masks[b], heights2D, slopes, dims, dc, detailRng,
                            result.Splatmap, result.TerrainLayers, treeDistMap, objDistMap);
                        allDetailProtos.AddRange(protos);
                        allDetailMaps.AddRange(maps);
                        PipelineProfiler.Lap($"Detail: GenerateForBiome ({biomeTypes[b]})");
                    }
                    result.DetailPrototypes = allDetailProtos;
                    result.DetailMaps = allDetailMaps;
                }

                // ===== Stage 6: Ore (world-global) =====
                if (config.generateOre)
                {
                    var allOreEntries = GenerateWorldOre(config, masks, biomeTypes, heights2D, res,
                        treeSpatialGrid, objectSpatialGrid);
                    result.OrePlacements = ConvertToObjectPlacements(allOreEntries);
                    Debug.Log($"[MapGenerator] Generated {result.OrePlacements.Count} ore placements.");
                    PipelineProfiler.Lap("Stage 6: Ore配置");
                }
            }
            else if (wantDetail)
            {
                var slopes = ComputeSlopes(heights, config, res);
                var allDetailProtos = new System.Collections.Generic.List<DetailPrototype>();
                var allDetailMaps = new System.Collections.Generic.List<int[,]>();
                for (int b = 0; b < biomeCount; b++)
                {
                    var dc = helper.GetDetailConfig(biomeTypes[b]);
                    if (dc?.entries == null || dc.entries.Length == 0) continue;
                    float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                    var dims = TerrainDimensions.From(config, wm);
                    var detailRng = new System.Random(config.seed + 6000 + b * 100);
                    var (protos, maps) = DetailPlacementGenerator.GenerateForBiome(
                        masks[b], heights2D, slopes, dims, dc, detailRng,
                        result.Splatmap, result.TerrainLayers, null, null);
                    allDetailProtos.AddRange(protos);
                    allDetailMaps.AddRange(maps);
                }
                result.DetailPrototypes = allDetailProtos;
                result.DetailMaps = allDetailMaps;
            }

            // wantObject == false でも鉱脈は独立して生成可能
            if (config.generateOre && result.OrePlacements == null)
            {
                var allOreEntries = GenerateWorldOre(config, masks, biomeTypes, heights2D, res,
                    null, null);
                result.OrePlacements = ConvertToObjectPlacements(allOreEntries);
                Debug.Log($"[MapGenerator] Generated {result.OrePlacements.Count} ore placements (no grid).");
            }
        }

        /// <summary>
        /// Stage 6: ワールド全体の鉱脈を配置する。各 OreEntry の出現バイオーム(biomes)から
        /// 合成マスクを構築し、OrePlacementGenerator に渡す。biomes 未指定のエントリ、および
        /// 対象バイオームが現在のワールドに存在しないエントリはマスク null となり配置されない。
        /// </summary>
        static System.Collections.Generic.List<PlacementEntry> GenerateWorldOre(
            TerrainGenerationConfig config,
            bool[][,] masks,
            BiomeType[] biomeTypes,
            float[,] heights2D,
            int res,
            Generators.Util.SpatialGrid treeSpatialGrid,
            Generators.Util.SpatialGrid objectSpatialGrid)
        {
            var ore = config.oreConfig;
            if (ore?.entries == null || ore.entries.Length == 0)
                return new System.Collections.Generic.List<PlacementEntry>();

            int biomeCount = biomeTypes.Length;
            var entries = ore.entries;
            var entryMasks = new bool[entries.Length][,];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                // 対象バイオーム未指定(None)はどこにも配置しない（マスク null）
                if (entry == null || entry.biomes == BiomeFlags.None)
                {
                    entryMasks[i] = null;
                    continue;
                }
                // 対象バイオーム群の合成マスク（OR）。該当バイオームが現在不在なら null のまま。
                bool[,] union = null;
                for (int b = 0; b < biomeCount; b++)
                {
                    if (!entry.biomes.Includes(biomeTypes[b])) continue;
                    var m = masks[b];
                    if (union == null) union = new bool[res, res];
                    for (int z = 0; z < res; z++)
                        for (int x = 0; x < res; x++)
                            if (m[z, x]) union[z, x] = true;
                }
                entryMasks[i] = union;
            }

            // 鉱石は waterMargin/ShoreMinHeight を参照しないため dims は1回構築でよい。
            var dims = TerrainDimensions.From(config, 0f);
            var oreRng = new System.Random(config.seed + 7000);
            return Generators.OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, ore.borderMargin, heights2D, dims, oreRng,
                treeSpatialGrid, objectSpatialGrid);
        }

        /// <summary>
        /// 配置データ（樹木/ディテール/オブジェクト）が1つでもあるか判定。
        /// ない場合はステージ3-5の重い変換処理を丸ごとスキップする。
        /// </summary>
        static bool HasAnyPlacement(TerrainGenerationConfig config,
            BiomePlacementHelper helper, BiomeType[] biomeTypes)
        {
            // 鉱脈はワールド全体で一元管理するため、バイオームループの外で判定する。
            if (config.generateOre && config.oreConfig?.entries != null
                && config.oreConfig.entries.Length > 0)
                return true;
            for (int b = 0; b < biomeTypes.Length; b++)
            {
                var tc = helper.GetTreePlacementConfig(biomeTypes[b]);
                if (tc != null && tc.prototypes != null && tc.prototypes.Length > 0)
                    return true;
                var dc = helper.GetDetailConfig(biomeTypes[b]);
                if (dc != null && dc.entries != null && dc.entries.Length > 0)
                    return true;
                var oc = helper.GetObjectConfig(biomeTypes[b]);
                if (oc != null && oc.entries != null && oc.entries.Length > 0)
                    return true;
            }
            return false;
        }

        // --- SpatialGrid構築ヘルパー ---

        // PlacementEntry[] → TreeInstance[] 変換
        static TreeInstance[] ConvertToTreeInstances(
            System.Collections.Generic.List<PlacementEntry> entries,
            System.Collections.Generic.Dictionary<GameObject, int> prefabToIndex,
            float terrainWidth, float terrainLength, float terrainHeight)
        {
            var instances = new System.Collections.Generic.List<TreeInstance>();
            foreach (var e in entries)
            {
                if (e.Prefab == null || !prefabToIndex.TryGetValue(e.Prefab, out int idx)) continue;
                instances.Add(new TreeInstance
                {
                    position = new Vector3(
                        e.WorldPosition.x / terrainWidth,
                        -e.Sink / terrainHeight,
                        e.WorldPosition.z / terrainLength),
                    widthScale = e.Scale.x,
                    heightScale = e.Scale.y,
                    prototypeIndex = idx,
                    rotation = e.Rotation.eulerAngles.y * Mathf.Deg2Rad,
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }
            return instances.ToArray();
        }

        // PlacementEntry[] → List<ObjectPlacementResult> 変換
        static System.Collections.Generic.List<ObjectPlacementResult> ConvertToObjectPlacements(
            System.Collections.Generic.List<PlacementEntry> entries)
        {
            var result = new System.Collections.Generic.List<ObjectPlacementResult>(entries.Count);
            foreach (var e in entries)
            {
                result.Add(new ObjectPlacementResult
                {
                    Prefab = e.Prefab,
                    Position = e.WorldPosition,
                    Rotation = e.Rotation,
                    Scale = e.Scale,
                    Sink = e.Sink,
                    ClusterInfo = e.Cluster ?? new RockClusterInfo { ClusterId = -1 }
                });
            }
            return result;
        }

        /// <summary>
        /// TreeInstance 配列からワールド座標の SpatialGrid を構築する。
        /// 距離マップ生成と Object 配置の距離制約で共用。
        /// </summary>
        static Generators.Util.SpatialGrid BuildTreeSpatialGrid(
            TreeInstance[] trees, float terrainWidth, float terrainLength)
        {
            if (trees == null || trees.Length == 0) return null;

            // セルサイズ: 平均間隔の2倍程度で効率的な検索になる
            float cellSize = Mathf.Max(terrainWidth / 50f, 5f);
            var grid = new Generators.Util.SpatialGrid(terrainWidth, terrainLength, cellSize);
            foreach (var tree in trees)
            {
                // TreeInstance.position は [0,1] 正規化座標
                grid.Add(tree.position.x * terrainWidth, tree.position.z * terrainLength);
            }
            return grid;
        }

        /// <summary>
        /// ObjectPlacementResult リストからチャンクローカル座標の SpatialGrid を構築する。
        /// TreePlacementのPoissonポイントはローカル座標のため、worldOffsetを引いて揃える。
        /// </summary>
        static Generators.Util.SpatialGrid BuildObjectSpatialGrid(
            System.Collections.Generic.List<Config.ObjectPlacementResult> objects,
            float terrainWidth, float terrainLength,
            float worldOffsetX = 0f, float worldOffsetZ = 0f)
        {
            if (objects == null || objects.Count == 0) return null;

            float cellSize = Mathf.Max(terrainWidth / 50f, 5f);
            var grid = new Generators.Util.SpatialGrid(terrainWidth, terrainLength, cellSize);
            foreach (var obj in objects)
            {
                // ワールド座標→ローカル座標に変換
                float localX = obj.Position.x - worldOffsetX;
                float localZ = obj.Position.z - worldOffsetZ;
                grid.Add(localX, localZ);
            }
            return grid;
        }

        // 旧ラッパーメソッドは削除済み。オーケストレーター(Generate)が直接GenerateForBiomeを呼ぶ。

        /// <summary>
        /// クラスター単位で裸地テクスチャを適用。per-biome の ObjectSurroundTextureConfig から
        /// レイヤー・半径・強度を取得し、内側コア＋外側遷移帯の2層マスクで自然な裸地化を行う。
        /// surroundLayer 未設定のバイオームは従来の Mud フォールバックを使用。
        /// </summary>
        static void ApplyObjectSurroundTexture(
            float[,,] splatmap, TerrainGenerationConfig config,
            UnityEngine.TerrainLayer[] terrainLayers,
            System.Collections.Generic.List<Config.ObjectPlacementResult> objects,
            BiomePlacementHelper helper, BiomeType[] biomeTypes,
            float[] heights = null,
            float[,] biomeWeights = null)
        {
            if (objects == null) return;

            int alphaRes = splatmap.GetLength(0);
            int layerCount = splatmap.GetLength(2);
            int hRes = config.Resolution;
            int biomeCount = biomeTypes.Length;

            // フォールバック: per-biome 設定に surroundLayer がなければ従来の Mud 検索
            UnityEngine.TerrainLayer fallbackMudLayer = null;
            foreach (var layer in terrainLayers)
            {
                if (layer != null && layer.name.Contains("Mud"))
                {
                    fallbackMudLayer = layer;
                    break;
                }
            }

            // クラスターIDでグループ化
            var clusterGroups = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Config.ObjectPlacementResult>>();
            var nonClusterObjects = new System.Collections.Generic.List<Config.ObjectPlacementResult>();

            foreach (var obj in objects)
            {
                if (obj.Prefab == null) continue;
                if (!obj.Prefab.name.Contains("Cliff") && !obj.Prefab.name.Contains("Boulder")) continue;

                int cid = obj.ClusterInfo.ClusterId;
                if (cid < 0)
                {
                    nonClusterObjects.Add(obj);
                    continue;
                }
                if (!clusterGroups.ContainsKey(cid))
                    clusterGroups[cid] = new System.Collections.Generic.List<Config.ObjectPlacementResult>();
                clusterGroups[cid].Add(obj);
            }

            // クラスター単位で裸地マスクを生成
            foreach (var kvp in clusterGroups)
            {
                var members = kvp.Value;
                if (members.Count == 0) continue;

                // クラスター中心のバイオームを判定し、per-biome 設定を取得
                var info = members[0].ClusterInfo;
                float centroidLX = info.Center.x - config.worldOffsetX;
                float centroidLZ = info.Center.z - config.worldOffsetZ;

                var stCfg = ResolveSurroundConfig(
                    centroidLX, centroidLZ, config, biomeWeights,
                    hRes, biomeCount, helper, biomeTypes);

                // 使用するレイヤーを決定（per-biome → フォールバック）
                UnityEngine.TerrainLayer activeLayer = stCfg.surroundLayer ?? fallbackMudLayer;
                if (activeLayer == null || !stCfg.enabled) continue;
                int layerIdx = System.Array.IndexOf(terrainLayers, activeLayer);
                if (layerIdx < 0) continue;

                // 各岩のローカル座標とスケールを収集してフットプリントを合成
                float minLX = float.MaxValue, maxLX = float.MinValue;
                float minLZ = float.MaxValue, maxLZ = float.MinValue;
                var footprints = new System.Collections.Generic.List<(float x, float z, float radius)>();

                foreach (var obj in members)
                {
                    float lx = obj.Position.x - config.worldOffsetX;
                    float lz = obj.Position.z - config.worldOffsetZ;
                    float footRadius = (obj.Scale.x + obj.Scale.z) * 0.5f * stCfg.rockMeshBaseSize;
                    footprints.Add((lx, lz, footRadius));

                    float expand = footRadius + stCfg.transitionRadius;
                    minLX = Mathf.Min(minLX, lx - expand);
                    maxLX = Mathf.Max(maxLX, lx + expand);
                    minLZ = Mathf.Min(minLZ, lz - expand);
                    maxLZ = Mathf.Max(maxLZ, lz + expand);
                }

                int pxMin = Mathf.Clamp(Mathf.FloorToInt(minLX / config.terrainWidth * (alphaRes - 1)), 0, alphaRes - 1);
                int pxMax = Mathf.Clamp(Mathf.CeilToInt(maxLX / config.terrainWidth * (alphaRes - 1)), 0, alphaRes - 1);
                int pzMin = Mathf.Clamp(Mathf.FloorToInt(minLZ / config.terrainLength * (alphaRes - 1)), 0, alphaRes - 1);
                int pzMax = Mathf.Clamp(Mathf.CeilToInt(maxLZ / config.terrainLength * (alphaRes - 1)), 0, alphaRes - 1);

                for (int pz = pzMin; pz <= pzMax; pz++)
                for (int px = pxMin; px <= pxMax; px++)
                {
                    float worldX = (float)px / (alphaRes - 1) * config.terrainWidth;
                    float worldZ = (float)pz / (alphaRes - 1) * config.terrainLength;

                    float minDist = float.MaxValue;
                    foreach (var (fx, fz, fr) in footprints)
                    {
                        float d = Mathf.Sqrt((worldX - fx) * (worldX - fx) + (worldZ - fz) * (worldZ - fz));
                        float edgeDist = d - fr;
                        if (edgeDist < minDist) minDist = edgeDist;
                    }
                    if (minDist > stCfg.transitionRadius) continue;

                    int hhx = Mathf.Clamp(Mathf.RoundToInt(worldX / config.terrainWidth * (hRes - 1)), 0, hRes - 1);
                    int hhz = Mathf.Clamp(Mathf.RoundToInt(worldZ / config.terrainLength * (hRes - 1)), 0, hRes - 1);
                    float slopeBias = ComputeDownhillBias(config, heights, hRes, hhx, hhz, worldX, worldZ, footprints);

                    // 2層ノイズ: per-biome の周波数・比率を使用
                    float noiseLow = Mathf.PerlinNoise(worldX * stCfg.noiseLowFrequency + 42.7f,
                        worldZ * stCfg.noiseLowFrequency + 18.3f);
                    float noiseHigh = Mathf.PerlinNoise(worldX * stCfg.noiseHighFrequency + 97.1f,
                        worldZ * stCfg.noiseHighFrequency + 63.5f);
                    float noiseMod = noiseLow * stCfg.noiseLowWeight + noiseHigh * (1f - stCfg.noiseLowWeight);

                    float biasedDist = minDist / slopeBias;

                    // 2層マスク: per-biome のコア/遷移帯半径・強度を使用
                    float blend = 0f;
                    if (biasedDist < stCfg.coreRadius)
                    {
                        float coreFactor = 1f - Mathf.Clamp01(biasedDist / stCfg.coreRadius);
                        blend = Mathf.Lerp(stCfg.coreBlendMin, stCfg.coreBlendMax, coreFactor)
                            * (0.7f + noiseMod * 0.3f);
                    }
                    else if (biasedDist < stCfg.transitionRadius)
                    {
                        float outerFactor = 1f - Mathf.Clamp01(
                            (biasedDist - stCfg.coreRadius) / (stCfg.transitionRadius - stCfg.coreRadius));
                        blend = Mathf.Lerp(stCfg.transitionBlendMin, stCfg.transitionBlendMax, outerFactor)
                            * noiseMod;
                    }

                    if (blend < 0.01f) continue;

                    float total = 0f;
                    for (int l = 0; l < layerCount; l++) total += splatmap[pz, px, l];
                    if (total < 0.001f) continue;

                    float remaining = 1f - blend;
                    for (int l = 0; l < layerCount; l++)
                    {
                        if (l == layerIdx) continue;
                        splatmap[pz, px, l] *= remaining;
                    }
                    splatmap[pz, px, layerIdx] += blend * total;
                }
            }

            // 非クラスターオブジェクトは per-biome 設定で単体裸地化
            foreach (var obj in nonClusterObjects)
            {
                float localX = obj.Position.x - config.worldOffsetX;
                float localZ = obj.Position.z - config.worldOffsetZ;

                var stCfg = ResolveSurroundConfig(
                    localX, localZ, config, biomeWeights,
                    hRes, biomeCount, helper, biomeTypes);

                UnityEngine.TerrainLayer activeLayer = stCfg.surroundLayer ?? fallbackMudLayer;
                if (activeLayer == null || !stCfg.enabled) continue;
                int layerIdx = System.Array.IndexOf(terrainLayers, activeLayer);
                if (layerIdx < 0) continue;

                float normX = localX / config.terrainWidth;
                float normZ = localZ / config.terrainLength;

                float radiusPx = stCfg.singleRockRadius / config.terrainWidth * alphaRes;
                int cx = Mathf.RoundToInt(normX * (alphaRes - 1));
                int cz = Mathf.RoundToInt(normZ * (alphaRes - 1));
                int r = Mathf.CeilToInt(radiusPx);

                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int px = cx + dx, pz = cz + dz;
                    if (px < 0 || px >= alphaRes || pz < 0 || pz >= alphaRes) continue;

                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist > radiusPx) continue;

                    float t = 1f - dist / radiusPx;
                    float noiseVal = Mathf.PerlinNoise(px * stCfg.noiseHighFrequency, pz * stCfg.noiseHighFrequency);
                    float blend = t * t * stCfg.singleRockBlend * (0.5f + noiseVal);

                    float total = 0f;
                    for (int l = 0; l < layerCount; l++) total += splatmap[pz, px, l];
                    if (total < 0.001f) continue;

                    for (int l = 0; l < layerCount; l++)
                    {
                        if (l == layerIdx) continue;
                        splatmap[pz, px, l] *= (1f - blend);
                    }
                    splatmap[pz, px, layerIdx] += blend * total;
                }
            }
        }

        /// <summary>
        /// ローカル座標からバイオームを判定し、そのバイオームの ObjectSurroundTextureConfig を返す。
        /// </summary>
        static Config.ObjectSurroundTextureConfig ResolveSurroundConfig(
            float localX, float localZ,
            TerrainGenerationConfig config, float[,] biomeWeights,
            int hRes, int biomeCount,
            BiomePlacementHelper helper, BiomeType[] biomeTypes)
        {
            if (biomeWeights == null || biomeCount == 0)
                return new Config.ObjectSurroundTextureConfig();

            float normX = localX / config.terrainWidth;
            float normZ = localZ / config.terrainLength;
            int hx = Mathf.Clamp(Mathf.RoundToInt(normX * (hRes - 1)), 0, hRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(normZ * (hRes - 1)), 0, hRes - 1);
            int idx = hz * hRes + hx;

            int bestBiome = 0;
            float maxW = 0f;
            for (int b = 0; b < biomeCount; b++)
            {
                float w = biomeWeights[idx, 2 + b];
                if (w > maxW) { maxW = w; bestBiome = b; }
            }

            return helper.GetSurroundTextureConfig(biomeTypes[bestBiome]);
        }

        /// <summary>
        /// 傾斜方向バイアス計算。下り方向のピクセルでは距離を圧縮し、Mudが延伸する。
        /// </summary>
        static float ComputeDownhillBias(
            TerrainGenerationConfig config, float[] heights, int hRes, int hx, int hz,
            float worldX, float worldZ,
            System.Collections.Generic.List<(float x, float z, float radius)> footprints)
        {
            if (heights == null) return 1f;

            // 地表の傾斜方向を計算
            float h = GetHeightSafe(heights, hRes, hx, hz);
            float hR = GetHeightSafe(heights, hRes, hx + 1, hz);
            float hU = GetHeightSafe(heights, hRes, hx, hz + 1);
            float dhdx = (hR - h) * config.terrainHeight;
            float dhdz = (hU - h) * config.terrainHeight;

            // 最近接フットプリントの方向
            float nearestDist = float.MaxValue;
            float dirX = 0f, dirZ = 0f;
            foreach (var (fx, fz, _) in footprints)
            {
                float dx = worldX - fx, dz = worldZ - fz;
                float d = dx * dx + dz * dz;
                if (d < nearestDist)
                {
                    nearestDist = d;
                    float len = Mathf.Sqrt(d) + 0.001f;
                    dirX = dx / len;
                    dirZ = dz / len;
                }
            }

            // 岩からの方向と下り方向の一致度（内積）
            float slopeLen = Mathf.Sqrt(dhdx * dhdx + dhdz * dhdz) + 0.001f;
            float slopeDirX = -dhdx / slopeLen;
            float slopeDirZ = -dhdz / slopeLen;
            float dot = dirX * slopeDirX + dirZ * slopeDirZ;

            // 下り方向に一致するほど距離を圧縮（1.0→1.5倍延伸）
            return 1f + Mathf.Clamp01(dot) * 0.5f;
        }

        static float GetHeightSafe(float[] heights, int hRes, int x, int z)
        {
            x = Mathf.Clamp(x, 0, hRes - 1);
            z = Mathf.Clamp(z, 0, hRes - 1);
            return heights[z * hRes + x];
        }

        /// <summary>
        /// バイナリマスクからの2乗ピクセル距離場を EDT (Felzenszwalb-Huttenlocher) で計算する。
        /// findSeed=true: マスク値>0.5のピクセルをシード（そこからの距離を計算）
        /// findSeed=false: マスク値≤0.5のピクセルをシード
        /// </summary>
        static float[] ComputeDistanceFieldSq(float[] mask, int res, bool findSeed)
        {
            const float INF = 1e10f;
            int n = res * res;
            var field = new float[n];

            // シードピクセル=0, 非シード=INF
            for (int i = 0; i < n; i++)
                field[i] = ((mask[i] > 0.5f) == findSeed) ? 0f : INF;

            // Pass 1: 行方向 EDT
            var temp = new float[n];
            Parallel.For(0, res, y =>
            {
                var v = new int[res];
                var z = new float[res + 1];
                EDT1D(field, y * res, res, temp, y * res, v, z);
            });

            // Pass 2: 列方向 EDT
            var result = new float[n];
            Parallel.For(0, res, x =>
            {
                var colIn = new float[res];
                var colOut = new float[res];
                var v = new int[res];
                var z = new float[res + 1];
                for (int y = 0; y < res; y++)
                    colIn[y] = temp[y * res + x];
                EDT1D(colIn, 0, res, colOut, 0, v, z);
                for (int y = 0; y < res; y++)
                    result[y * res + x] = colOut[y];
            });

            return result;
        }

        /// <summary>
        /// Felzenszwalb-Huttenlocher の1次元距離変換（ピクセル空間、スケール=1）。
        /// 入力 f[offset..offset+n-1] → 出力 d[dstOffset..dstOffset+n-1]。
        /// </summary>
        static void EDT1D(float[] f, int offset, int n, float[] d, int dstOffset,
            int[] v, float[] z)
        {
            v[0] = 0;
            z[0] = -1e10f;
            z[1] = 1e10f;
            int k = 0;

            for (int q = 1; q < n; q++)
            {
                float fq = f[offset + q];
                float s;
                while (true)
                {
                    int vk = v[k];
                    float fvk = f[offset + vk];
                    s = ((fq + (float)q * q) - (fvk + (float)vk * vk)) / (2f * (q - vk));
                    if (k == 0 || s > z[k]) break;
                    k--;
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = 1e10f;
            }

            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                int vk = v[k];
                float diff = q - vk;
                d[dstOffset + q] = diff * diff + f[offset + vk];
            }
        }

        static float Smoothstep01(float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }
    }
}
