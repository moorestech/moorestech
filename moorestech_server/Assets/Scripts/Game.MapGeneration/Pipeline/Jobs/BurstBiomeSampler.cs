using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// 全8バイオームのSampleHeight()をBurstコンパイル対応で再実装した静的クラス。
    /// HeightSampleJobから呼ばれ、BiomeParamsとnoiseOffsetsからバイオーム固有の高さを返す。
    /// 各メソッドはマネージド版(GrasslandBiome等)のSampleHeightと1:1対応する。
    /// </summary>
    // ジョブ内からのみ呼ばれるためクラスレベルの[BurstCompile]は不要。
    // Direct Call生成で float2 値渡しが BC1067 エラーを起こすため除去。
    public static class BurstBiomeSampler
    {

        /// <summary>
        /// BiomeType ordinalでディスパッチし、対応するバイオームの高さを返す。
        /// ジョブ内ループから毎ピクセル呼ばれるホットパス。
        /// </summary>
        public static float Sample(int biomeType, float2 pos, in BiomeParams p,
            in NativeArray<float2> offsets, int offsetBase)
        {
            switch (biomeType)
            {
                case 2: return SampleGrassland(pos, p, offsets, offsetBase);
                case 3: return SampleForest(pos, p, offsets, offsetBase);
                case 4: return SampleSavanna(pos, p, offsets, offsetBase);
                case 5: return SampleDesert(pos, p, offsets, offsetBase);
                case 6: return SampleMesa(pos, p, offsets, offsetBase);
                case 7: return SampleAlpine(pos, p, offsets, offsetBase);
                case 8: return SampleJungle(pos, p, offsets, offsetBase);
                case 9: return SampleWoods(pos, p, offsets, offsetBase);
                default: return 0f;
            }
        }

        // =========================================================================
        // Grassland: 単一Perlinノイズのみで低い丘陵を生成する。
        // 複雑な地形加工は一旦廃止し、調整の基準になる素直な高さ場に戻す。
        // =========================================================================

        static float SampleGrassland(float2 pos, in BiomeParams p,
            in NativeArray<float2> offsets, int offsetBase)
        {
            float2 baseOffset = offsetBase >= 0 && offsetBase < offsets.Length
                ? offsets[offsetBase]
                : float2.zero;
            float2 detailOffset = offsetBase + 1 >= 0 && offsetBase + 1 < offsets.Length
                ? offsets[offsetBase + 1]
                : float2.zero;

            // Stage 1: 草原全体の大きな起伏を単一Perlinで決める。
            float basePerlin = (noise.cnoise(pos * p.frequency + baseOffset) + 1f) * 0.5f;
            float height01 = basePerlin * p.amplitude;

            // Stage 2: 0中心の小さな凸凹を加え、全体の標高を底上げしない。
            if (p.secondaryAmplitude > 0.0001f && p.secondaryFrequency > 0.000001f)
            {
                float detailPerlin = (noise.cnoise(pos * p.secondaryFrequency + detailOffset) + 1f) * 0.5f;
                height01 += (detailPerlin - 0.5f) * p.secondaryAmplitude;
            }

            // Stage 3: Terrain正規化高さへスケールする。最終0-1制限はHeightSampleJob側で行う。
            return p.baseHeight + height01 * p.hillAmplitude;
        }

        // =========================================================================
        // Forest: ワープ→低周波ベース→べき乗→高さマスク付きリッジ＋ディテール
        // 高exponentで低地を黒に潰し、ピーク周辺にだけリッジを放射する山岳構造
        // =========================================================================

        static float SampleForest(float2 pos, in BiomeParams p,
            in NativeArray<float2> offsets, int offsetBase)
        {
            int offset = 0;

            // ドメインワープ: 有機的な山脈の形状を付与
            float2 warped = GenericWarpCoords(pos, p.frequency, p.domainWarpStrength,
                p.domainWarpIterations, offsets, offsetBase + offset);
            offset += p.domainWarpIterations * 2;

            // ベースFBm: 大きな山塊の骨格
            float fbm = BurstNoise.FBm(warped, p.frequency, offsets, offsetBase + offset,
                p.persistence, 2f, p.octaves);
            offset += p.octaves;

            // リッジノイズ: シャープな尾根構造をFBmの山に乗算で付与
            float ridge = BurstNoise.Ridged(warped,
                p.frequency * 2f, offsets, offsetBase + offset,
                2f, p.ridgeOctaves, 1f, 2f);
            offset += p.ridgeOctaves;

            // FBmが山の「どこに山があるか」を決め、リッジが山の「形状のシャープさ」を付与
            // 乗算方式: FBmが低い場所ではリッジの影響もゼロになる
            float baseTerrain = fbm * (1f + ridge * p.ridgeBlend);

            // 閾値カット+べき乗: FBm中間値以下を0に沈め、ピークだけ急峻に立ち上げる
            float cut = math.max(0f, baseTerrain - p.hillThreshold);
            float cutMax = math.max(1f - p.hillThreshold, 0.01f);
            float terrain = math.pow(math.saturate(cut / cutMax), p.exponent);

            // プラトー平坦化: 閾値付近でソフトクランプして自然な台地を形成
            // ハードクランプだと台地エッジが角張るため、smoothstepで丸みを付ける
            if (p.plateauFlatten > 0.001f)
            {
                float limit = p.plateauFlatten;
                if (terrain > limit * 0.7f)
                {
                    // limit*0.7から上をsmoothstepで緩やかにクランプ
                    float t = math.saturate((terrain - limit * 0.7f) / (limit * 0.6f));
                    float soft = t * t * (3f - 2f * t);
                    terrain = limit * 0.7f + soft * limit * 0.3f;
                }
            }

            // ディテール: 元座標で均一な凹凸を加算（高さに関係なく全域に適用）
            if (p.secondaryAmplitude > 0.001f)
            {
                float detail = BurstNoise.FBm(pos, p.secondaryFrequency, offsets, offsetBase + offset,
                    0.5f, 2f, p.canyonOctaves);
                terrain += detail * p.secondaryAmplitude;
            }
            offset += p.canyonOctaves;

            return p.baseHeight + math.saturate(terrain) * p.hillAmplitude;
        }

        // =========================================================================
        // Savanna: 平坦な平原 + smoothstep台地（マイクラ風サバンナ）
        // 狭帯域正規化で急峻な空間遷移を作り、smoothstepでS字成形
        // =========================================================================

        static float SampleSavanna(float2 pos, in BiomeParams p,
            in NativeArray<float2> offsets, int offsetBase)
        {
            // ドメインワープ: 台地の輪郭を歪ませて面白い境界を生成
            // warp成分を保存して平原の丘陵起伏にも再利用（追加FBm呼出を回避）
            float warpX = BurstNoise.FBm(pos, p.frequency * 2.5f, offsets, offsetBase, 0.5f, 2f, 3);
            float warpY = BurstNoise.FBm(pos, p.frequency * 2.5f, offsets, offsetBase + 1, 0.5f, 2f, 3);
            float2 warp = new float2(warpX - 0.5f, warpY - 0.5f) * 150f;

            // 台地位置ノイズ（ワープ後座標で不規則な輪郭を形成、周波数はInspectorから設定）
            float plateauNoise = BurstNoise.FBm(pos + warp, p.secondaryFrequency, offsets,
                offsetBase + 3, 0.5f, 2f, 4);

            // 表面ディテール用高周波ノイズ（テラス面の微細な凹凸）
            float detail = BurstNoise.FBm(pos, p.frequency * 8f, offsets, offsetBase,
                0.5f, 2f, 3);

            // 閾値以下は平原、以上を0-1正規化（境界の複雑さはドメインワープが担当）
            float above = math.max(0f, plateauNoise - p.hillThreshold)
                          / (1f - p.hillThreshold);

            // テラス量子化（マクロ構造 = 段々の骨格）
            float steps = (float)math.max(1, p.terraceSteps);
            float level = above * steps;
            float quantized = math.floor(level);
            float frac = level - quantized;

            // 段間遷移: 確実に歩行可能な傾斜（35度以下） + ノイズで変化
            float edgeWidth = 0.55f + detail * 0.2f;
            float edgeBlend = math.smoothstep(0f, edgeWidth, frac);
            float terraced = math.saturate((quantized + edgeBlend) / steps);

            // テラスと生ノイズをブレンド → テラス骨格をほぼ維持（頂上の平坦さ優先）
            float shaped = math.lerp(above, terraced, 0.98f);

            // 平原の丘陵起伏: 台地が上がるにつれ線形に減衰（堀の発生を防止）
            float undulation = warpX * p.exponent * (1f - shaped);

            // 表面ディテール: 高周波の微細な凹凸
            float surfaceDetail = detail * 0.008f;

            return p.baseHeight + undulation + surfaceDetail + shaped * p.hillAmplitude;
        }

        // =========================================================================
        // Desert: ベースfBm + 渓谷カービング + 崖リッジ加算
        // 5パラメータ版SampleFBm（offsetStartなし）を使うバイオーム
        // =========================================================================

        static float SampleDesert(float2 pos, in BiomeParams p,
            in NativeArray<float2> offsets, int offsetBase)
        {
            // ベース地形: 緩やかなfBmで砂漠の広域起伏を生成（5パラメータ版=offsetStart=0）
            float baseVal = BurstNoise.FBm(pos, p.frequency, offsets, offsetBase,
                0.5f, 2f, 3) * p.secondaryAmplitude;

            // 渓谷カービング: 平地の線アーティファクトの主原因なのでフルにスムーズ化
            float canyon = 1f;
            if (p.canyonDepth > 0.001f)
            {
                canyon = SimpleValleyNetwork(pos,
                    p.frequency * p.canyonFreqMult, p.canyonOctaves, p.canyonDepth,
                    offsets, offsetBase, p.absSmoothing);
            }

            // 崖: 稜線のシャープさを保つためスムーズ化を半分に抑える
            float cliff = 0f;
            if (p.ridgeBlend > 0.001f)
            {
                cliff = BurstNoise.Ridged(pos,
                    p.secondaryFrequency, offsets, offsetBase + 3 + p.canyonOctaves,
                    2f, p.ridgeOctaves, 1f, 2f, p.absSmoothing * 0.5f);
                cliff *= p.ridgeBlend;
            }

            return p.baseHeight + (baseVal * canyon) + cliff;
        }

        // =========================================================================
        // Mesa: ドメインワープ→積ノイズ(孤立化)→smoothstep→ディテール合成
        // 2つの独立fBm場の積で自然に孤立したビュートを生成。ゲーム品質準拠。
        // =========================================================================

        static float SampleMesa(float2 pos, in BiomeParams p,
            in NativeArray<float2> offsets, int offsetBase)
        {
            // Stage 1: ドメインワープでビュート輪郭を有機的に歪める
            // ワープ強度を周波数に応じて自動キャップ（折り畳みスパイク防止）
            float safeWarp = math.min(p.domainWarpStrength, 1f / (p.frequency * 200f));
            float2 warped = GenericWarpCoords(pos, p.frequency, safeWarp,
                p.domainWarpIterations, offsets, offsetBase);

            // メサ: ビュートパイプライン全スキップ。シンプルなFBmのみで砂漠の起伏を生成
            float butte = 0f;
            float talusHeight = 0f;
            float roughness = 0f;

            // 砂漠床面: 3スケールの符号付きFBmで自然な起伏
            // 大スケール: 広い砂丘のうねり
            float largeDune = (BurstNoise.FBm(pos, p.frequency * 0.8f, offsets,
                offsetBase, 0.45f, 2.2f, 3) - 0.5f) * p.hillAmplitude * 2f;
            // 中スケール: 侵食による凹凸
            float midDetail = (BurstNoise.FBm(pos, p.frequency * 3f, offsets,
                offsetBase + 4, 0.5f, 2f, 2) - 0.5f) * p.secondaryAmplitude * 0.8f;
            // 小スケール: 細かなザラつき
            float smallGrit = (BurstNoise.FBm(pos, p.frequency * 7f, offsets,
                offsetBase + 6, 0.5f, 2f, 2) - 0.5f) * p.secondaryAmplitude * 0.25f;
            float floorNoise = largeDune + midDetail + smallGrit;

            // Stage 8: 台地上ディテール — 風化した岩盤の微地形（台地上のみ）
            // ridgeBlend=台地上ノイズ強度、exponent=台地上ノイズ周波数倍率
            float topNoise = (BurstNoise.FBm(pos, p.frequency * p.exponent, offsets,
                offsetBase + p.octaves, 0.5f, 2f, 3) - 0.5f) * p.ridgeBlend * butte;

            float result = butte + talusHeight + roughness + floorNoise + topNoise;

            return p.baseHeight + math.saturate(result) * p.hillAmplitude;
        }

        // =========================================================================
        // Alpine: 単峰の火山塊 + 偏心した頂部 + 放射状リッジ
        // 粗いセルごとに火山中心を決め、極座標ベースのリッジで羽毛状の尾根を作る
        // =========================================================================

        static float SampleAlpine(float2 pos, in BiomeParams p,
            in NativeArray<float2> offsets, int offsetBase)
        {
            int offset = 0;

            // ワープはごく弱くし、流体模様を避ける
            float2 warped = GenericWarpCoords(pos, p.frequency,
                math.min(p.domainWarpStrength, 12f),
                p.domainWarpIterations, offsets, offsetBase + offset);
            offset += p.domainWarpIterations * 2;

            // 低周波セル内のランダム中心を火山の核として扱う。通常のterrainサイズでは
            // 1セルに収まりやすく、VolcanoMountainsのような単峰構図になりやすい。
            float coarseFrequency = math.max(p.frequency * 0.22f, 0.00012f);
            float2 coarsePos = warped * coarseFrequency;
            float2 seedOffset = offsets.Length > offsetBase
                ? offsets[offsetBase]
                : new float2(137f, 271f);
            int2 centerSeed = (int2)math.floor(seedOffset * 0.01f);
            float2 center = new float2(
                0.12f + math.frac(seedOffset.x * 0.0001f) * 0.12f,
                0.12f + math.frac(seedOffset.y * 0.0001f) * 0.12f);

            float2 toCenter = coarsePos - center;
            float dist = math.length(toCenter);
            float2 dir = dist > 0.0001f ? toCenter / dist : new float2(0f, 1f);
            float stretchAngle = CellHash01(centerSeed + new int2(71, 19)) * 6.2831853f;
            float2 stretchDir = new float2(math.cos(stretchAngle), math.sin(stretchAngle));

            // 山体の半径を少し揺らして左右非対称な裾野を作る
            float radiusJitter = BurstNoise.FBmRaw(warped, p.frequency * 0.35f,
                offsets, offsetBase + offset, 0.5f, 2f, 2);
            offset += 2;

            float cellRadius = 0.56f + CellHash01(centerSeed) * 0.12f + radiusJitter * 0.08f;
            float directionalStretch = 1f + math.dot(dir, stretchDir) * 0.32f;
            float radial = dist / math.max(cellRadius * directionalStretch, 0.3f);
            float mountainMask = 1f - math.smoothstep(0.08f, 0.94f, radial);

            // 低周波ベースで山塊のボリュームを作る
            float mass = BurstNoise.FBm(warped, p.frequency * 0.78f, offsets, offsetBase + offset,
                0.55f, 2.35f, p.octaves + 1);
            offset += p.octaves;

            float broadRidge = BurstNoise.Ridged(warped + stretchDir * 140f,
                p.frequency * 0.95f, offsets, offsetBase + offset,
                2.1f, p.ridgeOctaves, 1f, 1.9f, 0.09f);
            offset += p.ridgeOctaves;

            float cone = math.pow(math.saturate(1f - radial * 0.9f), 1.18f);
            // 台地化: 山頂中心のみコーン頂部を平坦にする（周辺ピークに漏れない）
            float plateauCap = p.secondaryFrequency;
            float pStr = p.plateauFlatten;
            // 狭いゾーン(radial<0.18)で強くcapし、コンパクトだが明確な台地を作る
            float coneCap = math.smoothstep(0.18f, 0.03f, radial) * pStr;
            if (coneCap > 0.001f && cone > plateauCap)
                cone = math.lerp(cone, plateauCap, coneCap * 0.95f);
            float skirt = math.pow(math.saturate(1f - radial * 0.68f), 0.9f);
            float blend = math.lerp(mass, broadRidge, p.ridgeBlend * 0.68f);
            float bulk = math.pow(math.saturate(blend), p.exponent * 1.02f);
            bulk *= cone + skirt * 0.34f;

            // 頂部を少し偏心させて双峰気味の強いハイライトを作る
            float summitOffset = 0.10f + CellHash01(centerSeed + new int2(43, 59)) * 0.10f;
            float2 secondCenter = center + stretchDir * summitOffset;
            float secondDist = math.distance(coarsePos, secondCenter);
            float2 ridgeA = center - stretchDir * (0.18f + CellHash01(centerSeed + new int2(9, 13)) * 0.12f);
            float2 ridgeB = center + stretchDir * (0.12f + CellHash01(centerSeed + new int2(27, 33)) * 0.18f);

            float primaryPeak = math.pow(math.saturate(1f - dist / 0.16f), p.exponent * 1.45f);
            float secondaryPeak = math.pow(math.saturate(1f - secondDist / 0.18f),
                p.exponent * 1.22f) * 0.26f;
            float summit = math.saturate(primaryPeak + secondaryPeak);
            // 台地周辺でサミットピークを抑制（中心で強く、外側で穏やかに→スパイク防止）
            if (pStr > 0.001f)
            {
                float summitSuppressZone = math.smoothstep(0.30f, 0.03f, radial);
                float summitKeep = math.lerp(1f, 0.25f, summitSuppressZone * pStr);
                summit *= summitKeep;
            }
            float radialMask = math.saturate((radial - 0.05f) / 0.58f)
                * math.pow(math.saturate(1f - radial), 0.98f);
            float highlandMask = math.pow(math.saturate(mountainMask), 1.08f);
            float midslopeMask = math.saturate((radial - 0.12f) / 0.22f)
                * math.saturate((0.88f - radial) / 0.38f);
            float asym = math.dot(dir, stretchDir) * 0.1f;

            float valley = SimpleValleyNetwork(warped, p.frequency * 2.2f,
                4, 0.12f, offsets, offsetBase + offset, 0.08f);
            offset += 4;

            float ridgeFine = BurstNoise.Ridged(warped + new float2(83f, -41f),
                p.frequency * 2.0f, offsets, offsetBase + offset,
                2f, 4, 1f, 2.15f, 0.03f);
            offset += 4;

            float midFbm = BurstNoise.FBm(warped + new float2(-53f, 97f),
                p.frequency * 1.15f, offsets, offsetBase + offset,
                0.55f, 2.15f, 4);
            offset += 4;

            float midRidge = BurstNoise.Ridged(warped + new float2(121f, 31f),
                p.frequency * 1.45f, offsets, offsetBase + offset,
                2.05f, 4, 1f, 1.95f, 0.05f);
            offset += 4;

            float ridgeLineDist = DistancePointToSegment(coarsePos, ridgeA, ridgeB);
            float ridgeLine = 1f - math.saturate(ridgeLineDist / 0.16f);

            float terrain = bulk;
            terrain *= math.lerp(1f, valley, radialMask * 0.18f);
            terrain += (broadRidge - 0.40f) * highlandMask * 0.22f;
            terrain += (ridgeFine - 0.5f) * radialMask * 0.015f;
            terrain += (midFbm - 0.5f) * highlandMask * 0.22f;
            terrain += (midRidge - 0.48f) * midslopeMask * 0.16f;
            terrain += ridgeLine * highlandMask * 0.24f;
            terrain += asym * highlandMask * 0.08f;

            // 低周波ノイズで放射方向の単調な傾斜を崩し、位置による雰囲気の偏りを軽減
            float broadVariation = BurstNoise.FBm(warped, p.frequency * 0.25f,
                offsets, offsetBase + offset, 0.5f, 2f, 2);
            offset += 2;
            terrain += (broadVariation - 0.5f) * highlandMask * 0.38f;

            float summitMask = math.pow(math.saturate(cone), 1.6f);
            terrain += (broadRidge - 0.5f) * summitMask * 0.16f;
            terrain += (ridgeFine - 0.5f) * summitMask * 0.08f;
            terrain += (1f - radialMask) * 0.04f;
            terrain = math.max(terrain, summit * 0.42f + bulk * 0.40f);
            // 天井: ピーク引き下げ（ceilHeight/ceilStrength で制御）
            if (pStr > 0.001f)
            {
                float capZone = math.smoothstep(0.40f, 0.04f, radial);
                float ceilBlend = capZone * pStr;
                if (ceilBlend > 0.001f && terrain > plateauCap)
                    terrain = math.lerp(terrain, plateauCap, ceilBlend * 0.88f);
            }
            // 床: 谷底引き上げ（floorHeight/floorStrength で独立制御）
            float fStr = p.floorStrength;
            if (fStr > 0.001f)
            {
                float floorZone = math.smoothstep(0.40f, 0.04f, radial);
                float floorBlend = floorZone * fStr;
                if (floorBlend > 0.001f && terrain < p.floorHeight)
                    terrain = math.lerp(terrain, p.floorHeight, floorBlend * 0.88f);
            }
            terrain = math.pow(math.saturate(terrain), 1.12f);

            // 台地ノイズ用のオフセットは消費済みとして飛ばす
            offset += 2;

            return p.baseHeight + terrain * p.hillAmplitude;
        }

        // =========================================================================
        // Jungle: Voronoiセル段配置 + ドメインワープ
        // Voronoiセルごとにハッシュで段レベルを割り当て、ワープで有機的に歪ませる
        // =========================================================================

        static float SampleJungle(float2 pos, in BiomeParams p,
            in NativeArray<float2> offsets, int offsetBase)
        {
            int steps = math.max(2, math.min(7, p.terraceSteps));
            int warpOct = math.max(1, p.octaves);

            // ドメインワープ: Voronoi境界を有機的に歪ませる
            float2 samplePos = pos;
            if (p.domainWarpStrength > 0f)
            {
                float wx = BurstNoise.FBmRaw(pos, p.frequency * 0.5f, offsets,
                    offsetBase, 0.5f, 2f, warpOct);
                float wz = BurstNoise.FBmRaw(pos, p.frequency * 0.5f, offsets,
                    offsetBase + warpOct, 0.5f, 2f, warpOct);
                samplePos = pos + new float2(wx, wz) * p.domainWarpStrength;
            }

            // Voronoiセル探索: セルID・距離・中心座標を取得
            int voronoiBase = offsetBase + warpOct * 2;
            BurstNoise.VoronoiCellEx(samplePos, p.frequency, offsets[voronoiBase],
                out int2 nearCell, out int2 secCell, out float d1, out float d2,
                out float2 c1, out float2 c2);

            // セルIDから段レベルをハッシュ割り当て + セル固有の高さオフセット
            int step1 = BurstNoise.CellToStep(nearCell, steps);
            int step2 = BurstNoise.CellToStep(secCell, steps);
            float h1 = (float)step1 / (steps - 1)
                        + BurstNoise.CellHeightHash(nearCell) * p.ridgeBlend;
            float h2 = (float)step2 / (steps - 1)
                        + BurstNoise.CellHeightHash(secCell) * p.ridgeBlend;

            // 境界スロープ: セル境界の接線方向に沿った座標でスロープ/崖を周期的に配置
            float slopeWidth = p.absSmoothing;
            float slopeRepeat = p.secondaryFrequency;
            float slopeCoverage = p.secondaryAmplitude;
            float terrain = h1;

            // 1段差の境界のみスロープ対象。2段以上の差は常に崖
            int stepDiff = math.abs(step1 - step2);
            float edgeDist = d2 - d1;
            if (stepDiff == 1 && slopeCoverage > 0f && slopeWidth > 0f && edgeDist < slopeWidth)
            {
                // 正準順序: 境界の両側で同じ方向・位相を得るためセルIDでソート
                bool swap = nearCell.x > secCell.x
                         || (nearCell.x == secCell.x && nearCell.y > secCell.y);
                int2 cellA = swap ? secCell : nearCell;
                int2 cellB = swap ? nearCell : secCell;
                float2 cA = swap ? c2 : c1;
                float2 cB = swap ? c1 : c2;

                float2 diff = cB - cA;
                float len = math.length(diff);
                if (len > 1e-6f)
                {
                    // 正準方向の接線（両側で同じ向き）
                    float2 tangent = new float2(-diff.y, diff.x) / len;
                    float2 mid = (cA + cB) * 0.5f;
                    float2 pScaled = (samplePos + offsets[voronoiBase]) * p.frequency;
                    float alongBoundary = math.dot(pScaled - mid, tangent);

                    // 正準ペアのハッシュ（順序不変）
                    int phaseHash = cellA.x * 198491317 + cellA.y * 781068421
                                  + cellB.x * 1136930381 + cellB.y * 852591037;
                    float phase = (phaseHash & 0x7FFFFFFF) / (float)0x7FFFFFFF * 2f * math.PI;

                    // sin値を連続マスクに変換: 0=崖（幅ゼロ）、1=スロープ（幅最大）
                    float sinVal = math.sin(alongBoundary * slopeRepeat * 2f * math.PI + phase);
                    float threshold = 1f - 2f * slopeCoverage;
                    float slopeMask = math.saturate((sinVal - threshold) / (1f - threshold));

                    // マスクで実効幅を変調: 崖=極小幅（シャープ）、スロープ=full幅
                    float effWidth = math.max(slopeWidth * slopeMask, 0.001f);
                    float blend = math.smoothstep(0f, effWidth, edgeDist);
                    terrain = h1 + (h2 - h1) * 0.5f * (1f - blend);
                }
            }

            // 表面ディテール: 元座標で低周波FBmを加減算し段上面の平坦さを崩す
            // edgeDistで境界マスク: セル内部（平坦面）でフル、境界（崖）付近でゼロ
            if (p.plateauFlatten > 0f)
            {
                float interiorMask = math.smoothstep(0f, slopeWidth * 0.5f, edgeDist);
                int detailBase = voronoiBase + 1;
                float detail = BurstNoise.FBm(pos, p.exponent, offsets,
                    detailBase, 0.5f, 2f, 2);
                terrain += (detail - 0.5f) * p.plateauFlatten * interiorMask;
            }

            // 境界ノイズはブラー後に別ジョブ(BoundaryNoiseJob)で追加

            return p.baseHeight + terrain * p.hillAmplitude;
        }

        // =========================================================================
        // Woods: fBm + NoiseSampler.Terrace（汎用テラス）
        // =========================================================================

        static float SampleWoods(float2 pos, in BiomeParams p,
            in NativeArray<float2> offsets, int offsetBase)
        {
            // 4オクターブfBm（5パラメータ版=offsetStart=0）
            float terrain = BurstNoise.FBm(pos, p.frequency, offsets, offsetBase,
                0.5f, 2f, 4);
            // NoiseSampler.Terrace相当: smoothstepベースの段丘化
            terrain = BurstNoise.Terrace(terrain, p.terraceSteps, p.terraceSharpness);
            return p.baseHeight + terrain * p.hillAmplitude;
        }

        // =========================================================================
        // 汎用ドメインワープ: NoiseSampler.WarpCoords相当
        // persistence=0.5f, lacunarity=2f, 5オクターブ固定
        // =========================================================================

        /// <summary>
        /// NoiseSampler.ValleyNetwork完全互換の谷ネットワーク。
        /// BurstNoise.ValleyNetworkはpow/min変換が入るため、Desert/Mesaには使えない。
        /// 返り値: 1f - valley * depth（valley=abs-noise minの最小追跡値）。
        /// </summary>
        static float SimpleValleyNetwork(float2 pos, float frequency,
            int octaves, float depth, in NativeArray<float2> offsets, int offsetBase,
            float smoothness = 0f)
        {
            float valley = 1f;
            float amp = 1f;
            float freq = frequency;
            for (int i = 0; i < octaves; i++)
            {
                // abs-noiseで谷パターンを生成。smoothness>0でゼロ交差の折り目を丸める
                float n = BurstNoise.SmoothAbs(
                    BurstNoise.FBmRaw(pos, freq, offsets, offsetBase + i, 0.5f, 2f, 1),
                    smoothness);
                valley = math.min(valley, n / amp);
                amp *= 0.5f;
                freq *= 2f;
            }
            return 1f - valley * depth;
        }

        /// <summary>
        /// 汎用ドメインワープ。FBmRawで座標を歪ませ、有機的な変形を生成する。
        /// NoiseSampler.WarpCoordsと同一ロジック。Mesa/Alpineで使用。
        /// </summary>
        static float2 GenericWarpCoords(float2 pos, float frequency, float strength,
            int iterations, in NativeArray<float2> offsets, int offsetBase)
        {
            float2 result = pos;
            for (int i = 0; i < iterations; i++)
            {
                // FBmRaw(3oct)でX/Z方向の変位を算出（低周波ワープには3octで十分）
                float wx = BurstNoise.FBmRaw(result, frequency, offsets,
                    offsetBase + i * 2, 0.5f, 2f, 3);
                float wz = BurstNoise.FBmRaw(result, frequency, offsets,
                    offsetBase + i * 2 + 1, 0.5f, 2f, 3);
                // 元座標ベースで加算しフィードバック発散を防ぐ
                result = pos + new float2(wx, wz) * strength;
            }
            return result;
        }

        static void FindNearestVolcanoCell(float2 pos, out float2 center, out int2 centerCell)
        {
            int2 cell = (int2)math.floor(pos);
            float minDistSq = float.MaxValue;
            center = pos;
            centerCell = cell;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 candidate = cell + new int2(dx, dz);
                    float2 feature = VolcanoFeaturePoint(candidate);
                    float distSq = math.lengthsq(pos - feature);
                    if (distSq < minDistSq)
                    {
                        minDistSq = distSq;
                        center = feature;
                        centerCell = candidate;
                    }
                }
            }
        }

        static float2 VolcanoFeaturePoint(int2 cell)
        {
            return (float2)cell + new float2(
                0.18f + CellHash01(cell) * 0.64f,
                0.18f + CellHash01(cell + new int2(53, 97)) * 0.64f);
        }

        static float CellHash01(int2 cell)
        {
            int h = cell.x * 374761393 + cell.y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        static float WrappedAngleDistance(float a, float b)
        {
            float d = math.abs(a - b);
            return math.min(d, 6.2831853f - d);
        }

        static float DistancePointToSegment(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float denom = math.max(math.dot(ab, ab), 1e-6f);
            float t = math.saturate(math.dot(p - a, ab) / denom);
            float2 q = a + ab * t;
            return math.distance(p, q);
        }
    }
}
