using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
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
}
