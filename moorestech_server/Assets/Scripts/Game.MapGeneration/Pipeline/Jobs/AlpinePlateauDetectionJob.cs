using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
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
}
