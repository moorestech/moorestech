using Unity.Collections;
using Unity.Mathematics;

namespace MapGenerator.Pipeline.Jobs
{
    /// <summary>
    /// Burst互換のバイオームパラメータ。各*BiomeConfigから変換され、ジョブに渡される。
    /// bool型はBurst blittability問題を回避するためintで表現。
    /// </summary>
    public struct BiomeParams
    {
        // 基本情報: BiomeType ordinalとClassifyPriorityでジョブ内分類ロジックを再現
        public int enabled;
        public int biomeType;
        public int classifyPriority;

        // 分類閾値: BiomeClassificationInputの3軸に対するフィルタ範囲
        public float temperatureMin, temperatureMax;
        public float elevationMin, elevationMax;
        public float humidityMin, humidityMax;

        // 高さ生成: baseHeight + SampleHeight() * hillAmplitude の基本構造
        public float baseHeight, hillAmplitude;
        public float frequency;
        public float amplitude;
        public int octaves;
        public float persistence, lacunarity;

        // ドメインワープ: 座標を歪めて有機的なパターンを形成
        public float domainWarpStrength;
        public int domainWarpIterations;
        public int domainWarpOctaves;

        // 段丘: FBM出力を離散段に量子化して段差地形を生成
        public int terraceEnabled;
        public int terraceSteps;
        public float terraceSharpness;
        public float terraceHeight;
        public float terraceBoundaryFreqMult;
        public float terraceBoundaryNoiseStrength;
        public int terraceBoundaryOctaves;

        // 渓谷: abs-noise minによる谷ネットワークカービング
        public int canyonEnabled;
        public float canyonDepth;
        public float canyonFreqMult;
        public int canyonOctaves;
        public float valleySharpness;

        // リッジ: edgeMaskベースの稜線加算
        public float ridgeBlend;
        public int ridgeOctaves;

        // abs()折り返しのスムーズ化。0=従来のabs、>0でゼロ交差の折り目を丸める
        public float absSmoothing;

        // ポスト処理: smoothstep平坦化とべき乗コントラスト
        public float plateauFlatten;
        public float exponent;

        // Alpine用: 床の高度と強度を天井と独立に制御
        public float floorHeight;
        public float floorStrength;

        // バイオーム固有の副次パラメータ（Forest/Savanna/Desert等で使用）
        public float secondaryFrequency;
        public float secondaryAmplitude;
        public float hillThreshold;

        // Splatmap: TerrainLayer配列内のインデックス（0はビーチ予約）
        public int splatmapLayerIndex;

        // noiseOffsets NativeArrayへのスライス情報（Base=開始、Count=個数）
        public int noiseOffsetBase;
        public int noiseOffsetCount;

        // textureEntries NativeArrayへのスライス情報
        public int textureEntryBase;
        public int textureEntryCount;

        // 海岸設定: 共通shoreConfigから変換
        public float waterMargin;
        public float shoreBeachElevation;
        public float beachThreshold;
        public float deepSeaThreshold;
        public float sandBlendThreshold;
        public int rockFallbackLayerIndex;

        // 境界設定: BiomeBoundaryConfigから変換
        public float heightBlendFastPathThreshold;
        public float heightBlendMinWeight;
        public float boundaryNoiseSmoothstepWidth;
        public float boundaryNoiseMidWeight;
        public float boundaryNoiseHighWeight;

        // プラトー設定: AlpinePlateauJob汎用化用
        public int enablePlateau;
        public int plateauSearchBaseRadius;
        public float plateauBoundaryBlend;
    }

    /// <summary>
    /// Splatmap用テクスチャエントリ。BiomeTextureConfig.TextureEntryから変換される。
    /// Job 2内でSplatmapFilter相当のフィルタリングに使用。
    /// </summary>
    public struct TextureEntryParams
    {
        // TerrainLayer配列でのインデックスとベースウェイト
        public int layerIndex;
        public float weight;

        // 傾斜フィルタ: 崖面テクスチャの切り替え
        public int useSlopeFilter;
        public float slopeMin, slopeMax;
        public float slopeSmoothness;

        // 高度フィルタ: 雪線・海岸ラインなど標高依存テクスチャ
        public int useHeightFilter;
        public float heightMin, heightMax;
        public float heightSmoothness;

        // 曲率フィルタ: 谷底に泥、尾根に露岩など凹凸ベースの配置
        public int useCurvatureFilter;
        public float curvatureMin, curvatureMax;
        public float curvatureSmoothness;

        // ノイズ変調: MapNoiseType ordinalとパラメータ
        public int noiseType;
        public float noiseFrequency;
        public float noiseAmplitude;
        public int noiseOffsetIndex;
    }

    /// <summary>
    /// ジョブで使用する全NativeArrayバッファを保持し、一括Disposeを提供する。
    /// TerrainGenerator内でusing/try-finallyで確実に解放すること。
    /// </summary>
    public struct JobBuffers : System.IDisposable
    {
        // パイプラインステージ1: 大陸形状とバイオーム分類
        public NativeArray<float> shoreMask;
        public NativeArray<float> landMask;
        // 陸側地形遷移（BeachTransitionJob生成 → HeightSampleJob消費）
        public NativeArray<float> beachFactor;
        // 砂浜近傍の陸側だけを単純平滑化する内部マスク
        public NativeArray<float> coastalSmoothFactor;
        // 陸側砂テクスチャ遷移（BeachTransitionJob生成 → SplatmapJob消費）
        public NativeArray<float> landTextureFactor;
        // 海側砂テクスチャ遷移（BeachTransitionJob生成 → SplatmapJob消費）
        public NativeArray<float> seaTextureFactor;
        public NativeArray<int> rawBiomeIndex;
        public NativeArray<float> rawBiomeWeights;

        // パイプラインステージ1c: ブラー後の最終バイオーム重み
        public NativeArray<float> biomeWeights;
        public NativeArray<int> winnerBiomeIndex;

        // パイプラインステージ2: 高さ・Splatmap出力
        public NativeArray<float> heights;
        public NativeArray<float> splatWeights;
        public NativeArray<float> blurTemp;

        // パイプラインステージ2b-2d: 台地化検出・領域分析・フラット化
        public NativeArray<float> plateauMask;
        public NativeArray<int> regionLabels;
        public NativeArray<PlateauRegionInfo> regionInfos;
        public NativeArray<int> regionCount;

        // 全ジョブ共有のReadOnlyデータ
        public NativeArray<float2> noiseOffsets;
        public NativeArray<BiomeParams> biomeParams;
        public NativeArray<TextureEntryParams> textureEntries;

        public void Dispose()
        {
            if (shoreMask.IsCreated) shoreMask.Dispose();
            if (landMask.IsCreated) landMask.Dispose();
            if (beachFactor.IsCreated) beachFactor.Dispose();
            if (coastalSmoothFactor.IsCreated) coastalSmoothFactor.Dispose();
            if (landTextureFactor.IsCreated) landTextureFactor.Dispose();
            if (seaTextureFactor.IsCreated) seaTextureFactor.Dispose();
            if (rawBiomeIndex.IsCreated) rawBiomeIndex.Dispose();
            if (rawBiomeWeights.IsCreated) rawBiomeWeights.Dispose();
            if (biomeWeights.IsCreated) biomeWeights.Dispose();
            if (winnerBiomeIndex.IsCreated) winnerBiomeIndex.Dispose();
            if (heights.IsCreated) heights.Dispose();
            if (splatWeights.IsCreated) splatWeights.Dispose();
            if (blurTemp.IsCreated) blurTemp.Dispose();
            if (plateauMask.IsCreated) plateauMask.Dispose();
            if (regionLabels.IsCreated) regionLabels.Dispose();
            if (regionInfos.IsCreated) regionInfos.Dispose();
            if (regionCount.IsCreated) regionCount.Dispose();
            if (noiseOffsets.IsCreated) noiseOffsets.Dispose();
            if (biomeParams.IsCreated) biomeParams.Dispose();
            if (textureEntries.IsCreated) textureEntries.Dispose();
        }
    }
}
