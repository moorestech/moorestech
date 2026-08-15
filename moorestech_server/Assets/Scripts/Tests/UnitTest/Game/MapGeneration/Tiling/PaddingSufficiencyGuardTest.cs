using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // クロップに必要な最小paddingと実効paddingの差を数値として固定する。config変更をどちらの方向でも検知させるための
    // ガードであり「足りている」ことの証明ではない（裁定: 2026-08-15-海岸系チャネルのパディング不足はガードテストで可視化し実機で判断する）。
    // Pins the gap between each channel's minimum required padding and the effective padding, so config drift trips this
    // test in either direction; it is not proof of sufficiency (ruling: 2026-08-15, visualize now, decide at Task 15's playtest).
    public class PaddingSufficiencyGuardTest
    {
        private const int Seed = 1;

        [Test]
        public void forUnitTestModの既定paddingは不足量が事実として固定されている()
        {
            var config = MultiTileTestWorld.BuildConfig(1, Seed);

            // PaddedWindowStage.cs:35 と同式。productionは変更しない方針のためテスト側に式を複製する
            // Same formula as PaddedWindowStage.cs:35; duplicated here since production stays untouched
            var effectivePadding = Mathf.Max(config.chunkPadding, config.biomeBlendRadius / 2);
            Assert.AreEqual(100, effectivePadding, "forUnitTestの実効paddingが変わった");

            var divisor = Mathf.Max(1, config.boundaryConfig.blurRadiusDivisor);

            // InterpolateWeightsJob(±blendRadius)→Horizontal/VerticalBlurJob(±blendRadius/divisor)の合算読み取り半径
            // Combined read radius: InterpolateWeightsJob(+-blendRadius) then Horizontal/VerticalBlurJob(+-blendRadius/divisor)
            var neededBiomeWeights = config.biomeBlendRadius + config.biomeBlendRadius / divisor;
            Assert.AreEqual(300, neededBiomeWeights);
            Assert.AreEqual(200, neededBiomeWeights - effectivePadding,
                "biomeWeights/winnerBiomeIndexの不足量。v8とは逆にforUnitTestではこちらが不足側");

            // BeachTransitionJobのEDT判定半径
            // BeachTransitionJob's EDT search radius
            var neededBeach = config.shoreConfig.beachLandTerrainRadius;
            Assert.AreEqual(10, neededBeach);
            Assert.LessOrEqual(neededBeach, effectivePadding, "beach系はforUnitTestでは充足しているはず");

            // coastalSmoothFactorのsmoothRadius = max(radius*2, radius+12)（BeachTransitionJob.cs:65と同式）
            // coastalSmoothFactor's smoothRadius = max(radius*2, radius+12) (same formula as BeachTransitionJob.cs:65)
            var neededCoastalHeights = Mathf.Max(neededBeach * 2, neededBeach + 12);
            Assert.AreEqual(22, neededCoastalHeights);
            Assert.LessOrEqual(neededCoastalHeights, effectivePadding, "coastal系heightsはforUnitTestでは充足しているはず");
        }

        // v8 production（別リポジトリ ../moorestech_master/server_v8）の値はテストから読めないため記録のみ:
        // chunkPadding=50・biomeBlendRadius=30・beachLandTerrainRadius=60・blurRadiusDivisor=2 → 実効50。
        // biomeWeights必要45(充足+5)・beach系必要60(不足10)・coastal系heights必要120(不足70)。Task15の実機判断待ち
        // v8 production values aren't readable from this test, recorded here only:
        // effective padding 50; biomeWeights needs 45 (+5 margin), beach needs 60 (-10 short), coastal heights needs 120 (-70 short). Deferred to Task 15's playtest.
    }
}
