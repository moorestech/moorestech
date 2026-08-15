using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // 各チャネルの必要到達を production とは別に書き起こし、PaddedWindowStage が導く実効padding がそれを満たすことを固定する。
    // 式を複製しているのが要点で、production 側の MaxReachPixels が痩せる方向に変わればこのテストが落ちる。
    // Restates each channel's required reach independently of production and pins that PaddedWindowStage's derived padding covers it.
    // Duplicating the formulas is the point: this test fails if production's MaxReachPixels ever shrinks.
    public class PaddingSufficiencyGuardTest
    {
        private const int Seed = 1;

        [Test]
        public void forUnitTestの導出paddingは全チャネルの必要到達を満たす()
        {
            var config = MultiTileTestWorld.BuildConfig(1, Seed);
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            try
            {
                // BeachTransitionJobは4半径をそれぞれ別チャネルへ書く。最大値を必要paddingとしないと、
                // beachLandTerrainRadius以外(例: beachSeaTextureRadius)が伸びるドリフトを検知できない
                // BeachTransitionJob writes each of its four radii to a different channel; taking anything less
                // than their max would miss drift where a radius other than beachLandTerrainRadius grows past it
                var shore = config.shoreConfig;
                var neededBeach = Mathf.Max(
                    Mathf.Max(shore.beachLandTextureRadius, shore.beachLandTerrainRadius),
                    Mathf.Max(shore.beachSeaTextureRadius, shore.beachSeaTerrainRadius));
                Assert.AreEqual(16, neededBeach);

                // coastalSmoothFactorはbeachLandTerrainRadiusのみを使う（BeachTransitionJob.cs:65）。
                // 上のneededBeach（4半径の最大）を流用すると別の式を検証したことになるため専用変数に分ける
                // coastalSmoothFactor reads only beachLandTerrainRadius (BeachTransitionJob.cs:65); reusing
                // neededBeach's max here would silently test a different formula, so it gets its own variable
                var landTerrainRadius = shore.beachLandTerrainRadius;
                var neededCoastal = Mathf.Max(landTerrainRadius * 2, landTerrainRadius + 12);
                Assert.AreEqual(22, neededCoastal);

                // InterpolateWeightsJob(±blendRadius)→Horizontal/VerticalBlurJob(±blendRadius/divisor)の合算読み取り半径
                // Combined read radius: InterpolateWeightsJob(+-blendRadius) then Horizontal/VerticalBlurJob(+-blendRadius/divisor)
                var divisor = Mathf.Max(1, config.boundaryConfig.blurRadiusDivisor);
                var neededWeights = config.biomeBlendRadius + config.biomeBlendRadius / divisor;
                Assert.AreEqual(300, neededWeights);

                var classificationReach = ClassificationStage.MaxReachPixels(config);
                Assert.AreEqual(Mathf.Max(Mathf.Max(neededBeach, neededCoastal), neededWeights), classificationReach,
                    "ClassificationStage.MaxReachPixels が3チャネルの最大と食い違っている");

                // 内訳は CoastalSmoothJob の 3x3 ぶんの1のみ。この設定は jungle を無効化するので BoundaryNoiseJob は走らず、
                // heightBlur(Jungle terraceSharpness) と slope(secondaryAmplitude) もどちらも0になる
                // The breakdown is just 1 for CoastalSmoothJob's 3x3: this setup disables jungle so BoundaryNoiseJob never runs,
                // and both heightBlur (Jungle terraceSharpness) and slope (secondaryAmplitude) come out zero
                var heightReach = HeightmapStage.MaxReachPixels(config, biomeParams);
                Assert.AreEqual(1, heightReach, "高さ後処理の連鎖到達が変わった");

                // 合成だけは production の実物を呼ぶ。テスト側に複製すると PaddedWindowStage が旧式へ戻されても気づけない
                // The composition alone calls the real production path; a duplicate here would not notice PaddedWindowStage reverting to the old formula
                var effectivePadding = PaddedWindowStage.ResolvePadding(config, biomeParams);
                Assert.AreEqual(301, effectivePadding, "forUnitTestの導出paddingが変わった");

                // 分類チャネルはそのまま、高さは後処理の連鎖ぶん外側の分類画素まで読むので合算を要求する
                // The classification channels need their own reach; heights additionally chain outward, so they need the sum
                Assert.LessOrEqual(neededBeach, effectivePadding, "beach系が不足している");
                Assert.LessOrEqual(neededCoastal, effectivePadding, "coastalSmoothFactorが不足している");
                Assert.LessOrEqual(neededWeights, effectivePadding, "biomeWeights/winnerBiomeIndexが不足している");
                Assert.LessOrEqual(classificationReach + heightReach, effectivePadding, "heightsの連鎖が不足している");
            }
            finally
            {
                biomeParams.Dispose();
            }
        }
    }
}
