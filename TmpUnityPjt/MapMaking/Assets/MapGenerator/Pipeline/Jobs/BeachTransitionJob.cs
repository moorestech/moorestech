using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapGenerator.Pipeline.Jobs
{
    /// <summary>
    /// EDT で事前計算された距離場からビーチ遷移帯を生成する。4つの半径で個別制御:
    /// 陸側テクスチャ(landTextureFactor)、陸側地形(beachFactor)、
    /// 海側テクスチャ(seaTextureFactor)、海側地形(shoreMask)。
    /// </summary>
    [BurstCompile]
    public struct BeachTransitionJob : IJobParallelFor
    {
        public int resolution;
        public int beachLandTextureRadius;
        public int beachLandTerrainRadius;
        public int beachSeaTextureRadius;
        public int beachSeaTerrainRadius;

        [ReadOnly] public NativeArray<float> landMask;
        // EDT で事前計算された2乗ピクセル距離
        [ReadOnly] public NativeArray<float> distToSeaSq;
        [ReadOnly] public NativeArray<float> distToLandSq;

        public NativeArray<float> shoreMask;
        public NativeArray<float> beachFactor;
        [WriteOnly] public NativeArray<float> coastalSmoothFactor;
        [WriteOnly] public NativeArray<float> landTextureFactor;
        [WriteOnly] public NativeArray<float> seaTextureFactor;

        public void Execute(int idx)
        {
            bool isLand = landMask[idx] > 0.5f;

            if (isLand)
            {
                float minDistSq = distToSeaSq[idx];
                float dist = math.sqrt(minDistSq);

                // 陸側地形遷移
                if (beachLandTerrainRadius > 0 && minDistSq <= beachLandTerrainRadius * beachLandTerrainRadius)
                {
                    float t = 1f - dist / beachLandTerrainRadius;
                    beachFactor[idx] = BurstTerrainMath.Smoothstep(0f, 1f, t);
                }
                else
                {
                    beachFactor[idx] = 0f;
                }

                // 陸側テクスチャ遷移
                if (beachLandTextureRadius > 0 && minDistSq <= beachLandTextureRadius * beachLandTextureRadius)
                {
                    float t = 1f - dist / beachLandTextureRadius;
                    landTextureFactor[idx] = BurstTerrainMath.Smoothstep(0f, 1f, t);
                }
                else
                {
                    landTextureFactor[idx] = 0f;
                }

                // 砂浜そのものより少し広い範囲だけを後段の単純平滑化対象にする
                int smoothRadius = math.max(beachLandTerrainRadius * 2, beachLandTerrainRadius + 12);
                if (smoothRadius > 0 && minDistSq <= smoothRadius * smoothRadius)
                {
                    float t = 1f - dist / smoothRadius;
                    coastalSmoothFactor[idx] = BurstTerrainMath.Smoothstep(0f, 1f, t);
                }
                else
                {
                    coastalSmoothFactor[idx] = 0f;
                }

                seaTextureFactor[idx] = 0f;
            }
            else
            {
                float minDistSq = distToLandSq[idx];
                float dist = math.sqrt(minDistSq);

                // 海側地形遷移（shoreMask）
                if (minDistSq <= beachSeaTerrainRadius * beachSeaTerrainRadius)
                {
                    float t = 1f - dist / beachSeaTerrainRadius;
                    shoreMask[idx] = BurstTerrainMath.Smoothstep(0f, 1f, t);
                    beachFactor[idx] = 1f;
                }
                else
                {
                    shoreMask[idx] = 0f;
                    beachFactor[idx] = 0f;
                }

                // 海側テクスチャ遷移
                if (beachSeaTextureRadius > 0 && minDistSq <= beachSeaTextureRadius * beachSeaTextureRadius)
                {
                    float t = 1f - dist / beachSeaTextureRadius;
                    seaTextureFactor[idx] = BurstTerrainMath.Smoothstep(0f, 1f, t);
                }
                else
                {
                    seaTextureFactor[idx] = 0f;
                }

                coastalSmoothFactor[idx] = 0f;
                landTextureFactor[idx] = 0f;
            }
        }
    }
}
