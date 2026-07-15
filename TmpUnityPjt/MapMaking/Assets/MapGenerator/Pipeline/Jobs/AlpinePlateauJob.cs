using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapGenerator.Pipeline.Jobs
{
    /// <summary>
    /// 台地化領域の共通メタデータ。PlateauRegionAnalysisJobが算出し、
    /// PlateauFlattenJobが参照する。1連結領域につき1インスタンス。
    /// </summary>
    public struct PlateauRegionInfo
    {
        public float targetHeight;
        public int pixelCount;
        public int boundaryCount;
    }

    /// <summary>
    /// Job 2b: ハイトマップ全体からピーク（コーン）を検出し台地化候補を決定する。
    /// HeightSampleJob完了後に実行。8方向×複数距離の方向別prominenceで、
    /// 全方位から突出しているドーム状のピークのみを台地化対象にする。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct AlpinePlateauDetectionJob : IJobParallelFor
    {
        public int resolution;
        public float prominenceThreshold;
        public int minProminentDirections;

        [ReadOnly] public NativeArray<float> heights;
        [ReadOnly] public NativeArray<int> winnerBiomeIndex;
        [ReadOnly] public NativeArray<BiomeParams> biomeParams;

        [WriteOnly] public NativeArray<float> plateauMask;

        const int DirectionCount = 8;

        public void Execute(int idx)
        {
            int winner = winnerBiomeIndex[idx];
            if (winner < 0 || biomeParams[winner].enablePlateau == 0)
            {
                plateauMask[idx] = 0f;
                return;
            }

            int x = idx % resolution;
            int y = idx / resolution;
            float h = heights[idx];

            int prominentCount = 0;
            float prominenceSum = 0f;

            for (int d = 0; d < DirectionCount; d++)
            {
                float angle = d * (2f * math.PI / DirectionCount);
                math.sincos(angle, out float sinA, out float cosA);

                float minH = h;
                for (int ri = 0; ri < 4; ri++)
                {
                    int radius = biomeParams[winner].plateauSearchBaseRadius << ri;
                    int sx = x + (int)math.round(radius * cosA);
                    int sy = y + (int)math.round(radius * sinA);

                    if (sx >= 0 && sx < resolution && sy >= 0 && sy < resolution)
                        minH = math.min(minH, heights[sy * resolution + sx]);
                }

                float dirProm = h - minH;
                if (dirProm > prominenceThreshold)
                {
                    prominentCount++;
                    prominenceSum += dirProm;
                }
            }

            plateauMask[idx] = prominentCount >= minProminentDirections
                ? prominenceSum / prominentCount
                : 0f;
        }
    }

    /// <summary>
    /// Job 2c: plateauMaskの連結領域をFlood Fillで検出し、領域ごとのメタデータを算出する。
    /// 最小サイズ・カバー率で不適格領域をフィルタし、受理領域のみにラベルを付与する。
    /// 領域レベルの情報（目標高度等）はregionInfosに格納し、全ジョブで共有する。
    /// </summary>
    [BurstCompile]
    public struct PlateauRegionAnalysisJob : IJob
    {
        public int resolution;
        public int minRegionSize;
        public float minCoverageRatio;
        public float coverageTolerance;

        [ReadOnly] public NativeArray<float> plateauMask;
        [ReadOnly] public NativeArray<float> heights;

        // 各ピクセルの所属領域ID（0=台地外、1〜=受理領域）
        public NativeArray<int> regionLabels;
        // 領域ごとの共通メタデータ（regionLabels値-1でインデックス）
        public NativeArray<PlateauRegionInfo> regionInfos;
        // 受理された領域数
        public NativeArray<int> regionCount;

        public void Execute()
        {
            int pixelCount = resolution * resolution;

            for (int i = 0; i < pixelCount; i++)
                regionLabels[i] = -1; // 未訪問

            var stack = new NativeList<int>(1024, Allocator.Temp);
            var regionPixels = new NativeList<int>(1024, Allocator.Temp);

            int acceptedRegions = 0;
            int maxRegions = regionInfos.Length;

            for (int i = 0; i < pixelCount; i++)
            {
                if (plateauMask[i] <= 0f)
                {
                    regionLabels[i] = 0;
                    continue;
                }
                if (regionLabels[i] != -1)
                    continue;

                // --- Flood Fill: 1つの連結領域を探索 ---
                stack.Clear();
                regionPixels.Clear();
                stack.Add(i);
                regionLabels[i] = 0; // 訪問済み仮マーク

                float boundaryHeightSum = 0f;
                int boundaryCount = 0;

                while (stack.Length > 0)
                {
                    int idx = stack[stack.Length - 1];
                    stack.RemoveAtSwapBack(stack.Length - 1);
                    regionPixels.Add(idx);

                    int x = idx % resolution;
                    int y = idx / resolution;

                    // 8近傍に非候補ピクセルがあれば境界
                    bool isBoundary = false;
                    for (int dy = -1; dy <= 1 && !isBoundary; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !isBoundary; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                            { isBoundary = true; break; }
                            if (plateauMask[ny * resolution + nx] <= 0f)
                                isBoundary = true;
                        }
                    }

                    if (isBoundary)
                    {
                        boundaryHeightSum += heights[idx];
                        boundaryCount++;
                    }

                    // 8方向に隣接する候補ピクセルを同一領域に追加
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < resolution && ny >= 0 && ny < resolution)
                                TryEnqueue(ny * resolution + nx, stack);
                        }
                    }
                }

                // --- 領域レベルの判定: サイズ・カバー率で受理/棄却 ---
                int regionSize = regionPixels.Length;
                if (regionSize < minRegionSize)
                {
                    // 小さすぎる領域は棄却
                    for (int j = 0; j < regionSize; j++)
                        regionLabels[regionPixels[j]] = 0;
                    continue;
                }

                float avgH = boundaryCount > 0
                    ? boundaryHeightSum / boundaryCount
                    : heights[i];

                // カバー率チェックは台地化後に事後検証するため、ここでは行わない

                // --- 仮受理: ラベル付与とメタデータ格納 ---
                if (acceptedRegions >= maxRegions) continue;
                acceptedRegions++;
                int regionId = acceptedRegions; // 1-based

                for (int j = 0; j < regionSize; j++)
                    regionLabels[regionPixels[j]] = regionId;

                regionInfos[regionId - 1] = new PlateauRegionInfo
                {
                    targetHeight = avgH,
                    pixelCount = regionSize,
                    boundaryCount = boundaryCount
                };
            }

            regionCount[0] = acceptedRegions;

            stack.Dispose();
            regionPixels.Dispose();
        }

        void TryEnqueue(int idx, NativeList<int> stack)
        {
            if (plateauMask[idx] > 0f && regionLabels[idx] == -1)
            {
                regionLabels[idx] = 0;
                stack.Add(idx);
            }
        }
    }

    /// <summary>
    /// Job 2d: 受理された台地領域を実際にフラット化する。
    /// regionLabelsで自ピクセルの所属領域を特定し、regionInfosから目標高度を取得。
    /// 境界からの距離に応じたsmoothstep遷移で、急な段差を防ぐ。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct PlateauFlattenJob : IJobParallelFor
    {
        public int resolution;
        public float baseTransition;
        public float transitionScale;
        // プラトー境界ピクセルのブレンド係数（BiomeBoundaryConfig由来）
        public float boundaryBlend;

        [ReadOnly] public NativeArray<int> regionLabels;
        [ReadOnly] public NativeArray<PlateauRegionInfo> regionInfos;
        [ReadOnly] public NativeArray<float> plateauMask;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> heights;

        public void Execute(int idx)
        {
            int regionId = regionLabels[idx];
            if (regionId <= 0) return;

            float target = regionInfos[regionId - 1].targetHeight;
            int x = idx % resolution;
            int y = idx / resolution;

            // 8近傍で境界判定し、targetに向かって50%ブレンドしてリムの芯を除去。
            // 4近傍だと斜め境界のリムが残るため8近傍で広く捕捉する
            bool isBoundary = false;
            for (int dy = -1; dy <= 1 && !isBoundary; dy++)
            {
                for (int dx = -1; dx <= 1 && !isBoundary; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                    { isBoundary = true; break; }
                    if (regionLabels[ny * resolution + nx] != regionId)
                        isBoundary = true;
                }
            }
            if (isBoundary)
            {
                heights[idx] = math.lerp(heights[idx], target, boundaryBlend);
                return;
            }

            // 8方向レイマーチで最近接の「同一領域の境界ピクセル」を探す
            float minDist = 9999f;
            float nearBndH = heights[idx];

            for (int d = 0; d < 8; d++)
            {
                int dx = 0, dy = 0;
                switch (d)
                {
                    case 0: dx = 1; dy = 0; break;
                    case 1: dx = 1; dy = 1; break;
                    case 2: dx = 0; dy = 1; break;
                    case 3: dx = -1; dy = 1; break;
                    case 4: dx = -1; dy = 0; break;
                    case 5: dx = -1; dy = -1; break;
                    case 6: dx = 0; dy = -1; break;
                    case 7: dx = 1; dy = -1; break;
                }
                float stepLen = (dx != 0 && dy != 0) ? 1.41421356f : 1f;

                for (int step = 1; step <= 128; step++)
                {
                    int sx = x + dx * step;
                    int sy = y + dy * step;
                    if (sx < 0 || sx >= resolution || sy < 0 || sy >= resolution)
                        break;

                    int si = sy * resolution + sx;
                    // 同一領域の外に出たら、1つ手前が境界
                    if (regionLabels[si] != regionId)
                    {
                        float dist = (step - 1) * stepLen;
                        if (dist < minDist)
                        {
                            minDist = dist;
                            int bx = x + dx * (step - 1);
                            int by = y + dy * (step - 1);
                            nearBndH = heights[by * resolution + bx];
                        }
                        break;
                    }
                }
            }

            float heightDiff = math.abs(nearBndH - target);
            float transWidth = baseTransition + heightDiff * transitionScale;
            float t = math.saturate(minDist / math.max(transWidth, 0.01f));
            t = t * t * (3f - 2f * t);

            heights[idx] = math.lerp(nearBndH, target, t);
        }
    }

    /// <summary>
    /// Job 2e: 台地化後のカバー率を事後検証し、基準未達の領域をロールバックする。
    /// Flatten適用後のheightsでカバー率を再計算し、minCoverageRatio未満なら
    /// heightsをバックアップから復元してregionLabelsを0にクリアする。
    /// </summary>
    [BurstCompile]
    public struct PlateauPostValidationJob : IJob
    {
        public int resolution;
        public float minCoverageRatio;
        public float coverageTolerance;

        [ReadOnly] public NativeArray<PlateauRegionInfo> regionInfos;
        [ReadOnly] public NativeArray<int> regionCount;
        // 台地化前の高さバックアップ
        [ReadOnly] public NativeArray<float> heightsBackup;

        public NativeArray<int> regionLabels;
        public NativeArray<float> heights;

        public void Execute()
        {
            int pixelCount = resolution * resolution;
            int nRegions = regionCount[0];

            for (int r = 0; r < nRegions; r++)
            {
                int regionId = r + 1;
                float target = regionInfos[r].targetHeight;

                // 台地化後のカバー率を計算
                int regionSize = 0;
                int nearCount = 0;
                for (int i = 0; i < pixelCount; i++)
                {
                    if (regionLabels[i] != regionId) continue;
                    regionSize++;
                    if (math.abs(heights[i] - target) <= coverageTolerance)
                        nearCount++;
                }

                if (regionSize == 0) continue;
                float coverage = (float)nearCount / regionSize;

                if (coverage >= minCoverageRatio) continue;

                // カバー率不足: ロールバック
                for (int i = 0; i < pixelCount; i++)
                {
                    if (regionLabels[i] != regionId) continue;
                    heights[i] = heightsBackup[i];
                    regionLabels[i] = 0;
                }
            }
        }
    }

    /// <summary>
    /// Job 2d-post-2: 台地内部スパイク除去。
    /// 同一領域のピクセルだけでボックスブラーし、局所平均からの逸脱が大きいスパイクのみ除去する。
    /// 台地外側の遷移はPlateauBoundaryRefineJobが担当するため、ここでは内部のみ処理する。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct PlateauBoundarySmoothJob : IJobParallelFor
    {
        public int resolution;
        public int kernelRadius;
        public float spikeThreshold;

        [ReadOnly] public NativeArray<int> regionLabels;
        [ReadOnly] public NativeArray<float> inputHeights;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> outputHeights;

        public void Execute(int idx)
        {
            int regionId = regionLabels[idx];
            if (regionId <= 0)
            {
                outputHeights[idx] = inputHeights[idx];
                return;
            }

            int x = idx % resolution;
            int y = idx / resolution;

            // 台地内部: 同一領域のピクセルだけでスパイク除去ブラー
            float sum = 0f;
            int sameCount = 0;
            for (int dy = -kernelRadius; dy <= kernelRadius; dy++)
            {
                for (int dx = -kernelRadius; dx <= kernelRadius; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                        continue;
                    int ni = ny * resolution + nx;
                    if (regionLabels[ni] == regionId)
                    {
                        sum += inputHeights[ni];
                        sameCount++;
                    }
                }
            }

            if (sameCount <= 1)
            {
                outputHeights[idx] = inputHeights[idx];
                return;
            }

            float localAvg = sum / sameCount;
            float deviation = math.abs(inputHeights[idx] - localAvg);
            float blend = math.saturate(deviation / spikeThreshold);
            outputHeights[idx] = math.lerp(inputHeights[idx], localAvg, blend);
        }
    }

    /// <summary>
    /// Job 2d-post-3: 台地境界帯のガウシアンスムージング＋パーリンノイズ付加。
    /// 8方向レイマーチで符号付き距離を算出し、帯域内のみガウシアンブラーで滑らかにする。
    /// その後、帯域中央付近にパーリンノイズを加えて自然な凹凸を与える。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct PlateauBoundaryRefineJob : IJobParallelFor
    {
        public int resolution;
        // 台地内側/外側の帯幅(px)。この範囲のピクセルだけ処理対象にする
        public int innerBand;
        public int outerBand;
        // ガウシアンカーネルのσ。帯域内で高さを滑らかに均す
        public float gaussSigma;
        // パーリンノイズの周波数・振幅上限・オクターブ数
        public float noiseFrequency;
        public float noiseAmplitude;
        public int noiseOctaves;
        // seed由来のオフセットでノイズパターンを固有化
        public float2 noiseOffset;

        [ReadOnly] public NativeArray<int> regionLabels;
        [ReadOnly] public NativeArray<float> inputHeights;
        [ReadOnly] public NativeArray<PlateauRegionInfo> regionInfos;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> outputHeights;

        public void Execute(int idx)
        {
            int x = idx % resolution;
            int y = idx / resolution;
            int myRegion = regionLabels[idx];

            // 8方向レイマーチで境界までの符号付き距離を算出（正=内側、負=外側）
            float signedDist = ComputeSignedDistance(x, y, myRegion);

            // 帯域外のピクセルはスキップ
            if (signedDist > innerBand || signedDist < -outerBand)
            {
                outputHeights[idx] = inputHeights[idx];
                return;
            }

            // ガウシアン重み付きブラー。カーネル半径はσの3倍で99.7%カバー
            int kernelR = (int)math.ceil(gaussSigma * 3f);
            float invTwoSigma2 = 1f / (2f * gaussSigma * gaussSigma);
            float weightedSum = 0f;
            float weightTotal = 0f;

            for (int dy = -kernelR; dy <= kernelR; dy++)
            {
                for (int dx = -kernelR; dx <= kernelR; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                        continue;
                    float w = math.exp(-(dx * dx + dy * dy) * invTwoSigma2);
                    weightedSum += inputHeights[ny * resolution + nx] * w;
                    weightTotal += w;
                }
            }

            float blurred = weightedSum / math.max(weightTotal, 1e-6f);

            // パーリンノイズ: 帯域中央(|d|≈2-4px)で最大、両端で0に減衰するsin²マスク
            float totalBand = innerBand + outerBand;
            float normalizedPos = (signedDist + outerBand) / math.max(totalBand, 1f);
            float noiseMask = math.sin(normalizedPos * math.PI);
            noiseMask *= noiseMask;

            // 局所高低差で振幅をスケーリング（平坦な境界に過剰ノイズを載せない）
            float localDelta = ComputeLocalHeightDelta(x, y);
            float clampedAmp = math.min(0.12f * localDelta, noiseAmplitude);

            float2 pos = new float2(x, y) + noiseOffset;
            float n = SampleFbm(pos * noiseFrequency, noiseOctaves);

            outputHeights[idx] = blurred + n * clampedAmp * noiseMask;
        }

        /// <summary>
        /// 8方向レイマーチで最も近い境界エッジまでの符号付き距離を返す。
        /// 正=台地内側からの距離、負=台地外側からの距離。
        /// </summary>
        float ComputeSignedDistance(int x, int y, int myRegion)
        {
            bool inside = myRegion > 0;
            int maxSearch = math.max(innerBand, outerBand) + 1;
            float minDist = maxSearch + 1f;

            for (int d = 0; d < 8; d++)
            {
                int dx = 0, dy = 0;
                switch (d)
                {
                    case 0: dx = 1; break;
                    case 1: dx = 1; dy = 1; break;
                    case 2: dy = 1; break;
                    case 3: dx = -1; dy = 1; break;
                    case 4: dx = -1; break;
                    case 5: dx = -1; dy = -1; break;
                    case 6: dy = -1; break;
                    case 7: dx = 1; dy = -1; break;
                }
                float stepLen = (dx != 0 && dy != 0) ? 1.41421356f : 1f;

                for (int step = 1; step <= maxSearch; step++)
                {
                    int sx = x + dx * step, sy = y + dy * step;
                    if (sx < 0 || sx >= resolution || sy < 0 || sy >= resolution)
                        break;

                    bool neighborInside = regionLabels[sy * resolution + sx] > 0;
                    if (inside != neighborInside)
                    {
                        // 境界は隣接ピクセルの中間にあると見なす
                        float dist = (step - 0.5f) * stepLen;
                        minDist = math.min(minDist, dist);
                        break;
                    }
                }
            }

            return inside ? minDist : -minDist;
        }

        /// <summary>
        /// 3px近傍の高低差。ノイズ振幅のスケーリングに使う。
        /// </summary>
        float ComputeLocalHeightDelta(int x, int y)
        {
            float hMin = inputHeights[y * resolution + x];
            float hMax = hMin;
            const int r = 3;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                        continue;
                    float h = inputHeights[ny * resolution + nx];
                    hMin = math.min(hMin, h);
                    hMax = math.max(hMax, h);
                }
            }
            return hMax - hMin;
        }

        float SampleFbm(float2 p, int octaves)
        {
            float sum = 0f, amp = 1f, freq = 1f, totalAmp = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += noise.snoise(p * freq) * amp;
                totalAmp += amp;
                freq *= 2f;
                amp *= 0.5f;
            }
            return sum / math.max(totalAmp, 1e-6f);
        }
    }

    /// <summary>
    /// デバッグ用: 台地候補をスプラットマップで可視化する。
    /// 近傍ピクセルの同一領域比率でフェードし、ピクセル境界のギザギザを防ぐ。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct PlateauDebugOverlayJob : IJobParallelFor
    {
        public int resolution;
        public int totalLayers;
        public int baseLayerIndex;
        public int debugLayerStart;
        public int debugLayerCount;
        // フェード判定の近傍半径(px)
        public int fadeRadius;

        [ReadOnly] public NativeArray<float> plateauMask;
        [ReadOnly] public NativeArray<int> regionLabels;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> splatWeights;

        public void Execute(int idx)
        {
            int regionId = regionLabels[idx];

            if (regionId > 0 && debugLayerCount > 0)
            {
                int x = idx % resolution;
                int y = idx / resolution;

                // fadeRadius 内の同一領域比率で滑らかなフェードを算出
                int sameCount = 0;
                int totalCount = 0;
                for (int dy = -fadeRadius; dy <= fadeRadius; dy++)
                {
                    for (int dx = -fadeRadius; dx <= fadeRadius; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                            continue;
                        totalCount++;
                        if (regionLabels[ny * resolution + nx] == regionId)
                            sameCount++;
                    }
                }

                float alpha = (float)sameCount / totalCount;
                // 低比率はフェード中、高比率はほぼ不透明
                alpha = alpha * alpha;
                if (alpha < 0.01f) return;

                int baseIdx = idx * totalLayers;
                int layer = debugLayerStart + ((regionId - 1) % debugLayerCount);
                if (layer < 0 || layer >= totalLayers) return;

                for (int l = 0; l < totalLayers; l++)
                {
                    float existing = splatWeights[baseIdx + l];
                    float target = (l == layer) ? 1f : 0f;
                    splatWeights[baseIdx + l] = math.lerp(existing, target, alpha);
                }
                return;
            }

            // 棄却候補: ベースレイヤーで表示
            if (plateauMask[idx] > 0f)
            {
                int baseIdx = idx * totalLayers;
                for (int l = 0; l < totalLayers; l++)
                    splatWeights[baseIdx + l] = 0f;
                if (baseLayerIndex >= 0 && baseLayerIndex < totalLayers)
                    splatWeights[baseIdx + baseLayerIndex] = 1f;
            }
        }
    }
}
