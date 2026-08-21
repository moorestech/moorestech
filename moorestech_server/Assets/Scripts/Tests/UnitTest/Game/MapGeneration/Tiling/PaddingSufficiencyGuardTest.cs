using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // 分類チャネルの必要到達を production とは別に書き起こし、PaddedWindowStage が導く実効padding がそれを満たすことを固定する。
    // 分類側は式を複製しているので ClassificationWindowReach が痩せれば落ちる。高さ側は式を複製できない
    // （半径の抽出が HeightmapStage の private）ため、加算3項が全て効く設定で合計値を直接ピン留めして代替している。
    // Restates the classification channels' required reach independently of production and pins that the derived padding covers it.
    // The classification side duplicates the formulas, so it fails if ClassificationWindowReach shrinks. The height side cannot be
    // duplicated (its radius extraction is private to HeightmapStage), so instead it pins the total on a config where all three added terms are live.
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

                var classificationReach = ClassificationWindowReach.Pixels(config);
                Assert.AreEqual(Mathf.Max(Mathf.Max(neededBeach, neededCoastal), neededWeights), classificationReach,
                    "ClassificationWindowReach.Pixels が3チャネルの最大と食い違っている");

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

        // 上のテストの設定は加算3項が全て0なので、3項を丸ごと削除しても通ってしまう。
        // jungle のテラス・砂漠のスロープ・jungle の境界ノイズを全て効かせた設定で、内訳ごと合計を固定する。
        // The test above zeroes all three added terms, so deleting them outright would still pass it.
        // This one turns on jungle's terrace, the desert slope, and jungle's boundary noise, pinning the total together with its breakdown.
        [Test]
        public void 高さ後処理の到達は加算3項が全て効く設定で内訳どおりになる()
        {
            var config = BuildAllTermsLiveConfig();
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            try
            {
                // 内訳: CoastalSmoothJob の 3x3 が1、HeightBlur が terraceSharpness*20 = 10、
                // HeightSlope が desert の canyonOctaves = 4、BoundaryNoise の勾配が1
                // Breakdown: 1 for CoastalSmoothJob's 3x3, 10 for HeightBlur (terraceSharpness*20),
                // 4 for HeightSlope (the desert canyonOctaves), and 1 for BoundaryNoise's gradient
                Assert.AreEqual(1 + 10 + 4 + 1, HeightmapStage.MaxReachPixels(config, biomeParams),
                    "加算3項のどれかが落ちているか、ゲート条件が RunHeightPostProcess とずれている");
            }
            finally
            {
                biomeParams.Dispose();
            }
        }

        // HeightBlur は Jungle(8) の terraceSharpness、HeightSlope は canyonOctaves と secondaryAmplitude と
        // absSmoothing の3つ揃い、BoundaryNoise は jungleEnabled と boundaryNoiseStrength でそれぞれゲートされる。
        // HeightBlur gates on Jungle(8)'s terraceSharpness, HeightSlope on canyonOctaves plus secondaryAmplitude plus
        // absSmoothing all being set, and BoundaryNoise on jungleEnabled together with boundaryNoiseStrength.
        private static TerrainGenerationConfig BuildAllTermsLiveConfig()
        {
            var config = MultiTileTestWorld.BuildConfig(1, Seed);

            config.jungleEnabled = true;
            config.jungle.transitionSmoothing = 0.5f;
            config.jungle.boundaryNoiseStrength = 40f;

            // desert は Jungle より後ろに並ぶが canyonOctaves を持つ唯一の有効バイオームになるので GetSlopeParams が拾う
            // Desert sorts after Jungle but becomes the only enabled biome carrying canyonOctaves, so GetSlopeParams picks it up
            config.desertEnabled = true;
            config.desert.canyonOctaves = 4;
            config.desert.duneAmplitude = 0.025f;
            config.desert.absSmoothing = 0.25f;
            return config;
        }
    }
}
