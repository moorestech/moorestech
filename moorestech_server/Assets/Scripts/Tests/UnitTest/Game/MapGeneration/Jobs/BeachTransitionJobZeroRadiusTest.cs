using Game.MapGeneration.Pipeline.Jobs;
using NUnit.Framework;
using Unity.Collections;

namespace Tests.UnitTest.Game.MapGeneration.Jobs
{
    // 半径0のマスタで海側地形遷移が無効のままであることを固定する。距離0の海画素は 1 - 0/0 を踏み、
    // saturateがNaNを1へ丸めるので shoreMask/beachFactor が最大強度で立ち、HeightSampleJob の高さまで動く。
    // Pins that the sea-side terrain transition stays off under a zero-radius master: a zero-distance sea pixel hits 1 - 0/0
    // and saturate folds the NaN to 1, standing shoreMask/beachFactor at full strength and moving HeightSampleJob's heights.
    public class BeachTransitionJobZeroRadiusTest
    {
        private const int Resolution = 4;

        [Test]
        public void 海側地形半径0のマスタでshoreMaskにNaNが入らない()
        {
            var pixelCount = Resolution * Resolution;
            var landMask = new NativeArray<float>(pixelCount, Allocator.TempJob);
            var distToSeaSq = new NativeArray<float>(pixelCount, Allocator.TempJob);
            var distToLandSq = new NativeArray<float>(pixelCount, Allocator.TempJob);
            var shoreMask = new NativeArray<float>(pixelCount, Allocator.TempJob);
            var beachFactor = new NativeArray<float>(pixelCount, Allocator.TempJob);
            var coastalSmoothFactor = new NativeArray<float>(pixelCount, Allocator.TempJob);
            var landTextureFactor = new NativeArray<float>(pixelCount, Allocator.TempJob);
            var seaTextureFactor = new NativeArray<float>(pixelCount, Allocator.TempJob);

            // 全画素を海(landMask=0)にし、陸までの距離も0にする。ClassificationDistanceFieldの閾値一致で実際に起きる組み合わせ
            // Every pixel is sea (landMask 0) with zero distance to land, the very combination ClassificationDistanceField's matching threshold produces
            for (var idx = 0; idx < pixelCount; idx++)
            {
                landMask[idx] = 0f;
                distToSeaSq[idx] = 0f;
                distToLandSq[idx] = 0f;
                shoreMask[idx] = 1f;
                beachFactor[idx] = 1f;
                coastalSmoothFactor[idx] = 1f;
            }

            var job = new BeachTransitionJob
            {
                resolution = Resolution,
                beachLandTextureRadius = 0,
                beachLandTerrainRadius = 0,
                beachSeaTextureRadius = 0,
                beachSeaTerrainRadius = 0,
                landMask = landMask,
                distToSeaSq = distToSeaSq,
                distToLandSq = distToLandSq,
                shoreMask = shoreMask,
                beachFactor = beachFactor,
                coastalSmoothFactor = coastalSmoothFactor,
                landTextureFactor = landTextureFactor,
                seaTextureFactor = seaTextureFactor,
            };

            // Executeを直接回す。ジョブスケジューラを挟むとBurstのキャッシュ次第で古い実装が走り、判定が信用できない
            // Execute is driven directly: going through the scheduler can run a Burst-cached older build and make the verdict untrustworthy
            for (var idx = 0; idx < pixelCount; idx++) job.Execute(idx);

            for (var idx = 0; idx < pixelCount; idx++)
            {
                // 海画素で無条件に0が書かれるチャネル。ここが1のままならジョブ自体が走っていない
                // The channel written unconditionally to zero on a sea pixel; still 1 here means the job never ran at all
                Assert.AreEqual(0f, coastalSmoothFactor[idx], $"coastalSmoothFactor[{idx}]が0でない: {coastalSmoothFactor[idx]}");

                Assert.AreEqual(0f, shoreMask[idx], $"shoreMask[{idx}]が0でない: {shoreMask[idx]}");
                Assert.AreEqual(0f, beachFactor[idx], $"beachFactor[{idx}]が0でない: {beachFactor[idx]}");
            }

            landMask.Dispose();
            distToSeaSq.Dispose();
            distToLandSq.Dispose();
            shoreMask.Dispose();
            beachFactor.Dispose();
            coastalSmoothFactor.Dispose();
            landTextureFactor.Dispose();
            seaTextureFactor.Dispose();
        }
    }
}
