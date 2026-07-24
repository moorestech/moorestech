using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// ブラー済みハイトマップの段差境界を侵食ノイズで削る後処理。
    /// 勾配で崖面を検出し、abs(fBm)で常に減算して自然な風化パターンを作る。
    /// HeightBlur→本ジョブ→SplatmapJobの順で実行する。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct BoundaryNoiseJob : IJobParallelFor
    {
        public int resolution;
        public float terrainWidth;
        public float terrainLength;
        public float terrainHeight;
        // ノイズ強度（0=無し、0.5=中程度、1=強い）
        public float noiseStrength;
        // ノイズが効き始める勾配角度（度）。これ以上の急斜面にノイズが入る
        public float slopeThreshold;
        // ワールド空間でのノイズ周波数
        public float noiseFrequency;
        // シード値（再現性のため）
        public float seed;
        // smoothstep遷移帯幅（度）。slopeThresholdからこの幅で0→1に遷移
        public float smoothstepWidth;
        // 2帯域ノイズの混合比
        public float noiseMidWeight;
        public float noiseHighWeight;

        // 読み取り専用（ブラー後の元データ）
        [ReadOnly] public NativeArray<float> readHeights;
        // 書き込み先
        [NativeDisableParallelForRestriction]
        public NativeArray<float> heights;

        // 行単位で並列実行
        public void Execute(int y)
        {
            if (noiseStrength <= 0f) return;

            float cellX = terrainWidth / (resolution - 1);
            float cellZ = terrainLength / (resolution - 1);

            for (int x = 0; x < resolution; x++)
            {
                int idx = y * resolution + x;

                // 隣接ピクセルとの勾配で境界を検出
                int xR = math.min(x + 1, resolution - 1);
                int xL = math.max(x - 1, 0);
                int yU = math.min(y + 1, resolution - 1);
                int yD = math.max(y - 1, 0);
                float dhdx = math.abs(readHeights[y * resolution + xR] - readHeights[idx])
                             * terrainHeight / cellX;
                float dhdz = math.abs(readHeights[yU * resolution + x] - readHeights[idx])
                             * terrainHeight / cellZ;
                float slopeAngle = math.atan(math.sqrt(dhdx * dhdx + dhdz * dhdz))
                                   * math.TODEGREES;

                // 閾値以上の急斜面にのみノイズ適用
                float mask = math.smoothstep(slopeThreshold, slopeThreshold + smoothstepWidth, slopeAngle);
                if (mask < 0.001f) continue;

                // ワールド座標でのノイズ生成
                float wx = (float)x / (resolution - 1) * terrainWidth;
                float wz = (float)y / (resolution - 1) * terrainLength;
                float2 noisePos = new float2(wx + seed, wz + seed * 0.7f);

                // 2帯域ノイズ: 中周波（凹凸）+ 高周波（ザラつき）
                float n1 = Noise(noisePos * noiseFrequency) * noiseMidWeight;
                float n2 = Noise(noisePos * noiseFrequency * 3f) * noiseHighWeight;
                float erode = math.abs(n1 + n2 - 0.5f) * noiseStrength * mask;
                float eroded = readHeights[idx] - erode / terrainHeight;

                // 隣接最小値でクランプ: 崖下の谷底より低くならない→穴を防止
                float minNeighbor = math.min(
                    math.min(readHeights[y * resolution + xR], readHeights[y * resolution + xL]),
                    math.min(readHeights[yU * resolution + x], readHeights[yD * resolution + x]));
                heights[idx] = math.max(eroded, minNeighbor);
            }
        }

        // Burst互換の簡易Perlinノイズ（BurstNoise.Perlinと同等）
        static float Noise(float2 p)
        {
            int2 i = (int2)math.floor(p);
            float2 f = p - math.floor(p);
            float2 u = f * f * (3f - 2f * f);
            float a = Hash2(i);
            float b = Hash2(i + new int2(1, 0));
            float c = Hash2(i + new int2(0, 1));
            float d = Hash2(i + new int2(1, 1));
            return math.lerp(math.lerp(a, b, u.x), math.lerp(c, d, u.x), u.y);
        }

        static float Hash2(int2 p)
        {
            int h = p.x * 374761393 + p.y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }
}
