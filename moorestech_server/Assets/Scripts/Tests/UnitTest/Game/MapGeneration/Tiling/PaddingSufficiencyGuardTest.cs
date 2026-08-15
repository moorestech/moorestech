using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // クロップに必要な最小paddingと実効paddingの差を数値として固定する。config変更をどちらの方向でも検知させるための
    // ガードであり「足りている」ことの証明ではない。数値の一次情報は
    // .decisions/2026-08-15-海岸系チャネルのパディング不足はガードテストで可視化し実機で判断する.md に一本化する
    // （v8 productionの数値もそこに記載。ここでは再掲しない）
    // Pins the gap between each channel's minimum required padding and the effective padding, so config drift trips
    // this test in either direction; it is not proof of sufficiency. The numbers' single source of truth is
    // .decisions/2026-08-15-...md (v8 production's values live there too; not repeated here)
    public class PaddingSufficiencyGuardTest
    {
        private const int Seed = 1;

        [Test]
        public void forUnitTestModの既定paddingは不足量が事実として固定されている()
        {
            var config = MultiTileTestWorld.BuildConfig(1, Seed);

            // PaddedWindowStage.cs:35 と同式。productionは変更しない方針のためテスト側に式を複製しており、
            // production側の式そのものが変わってもこのガードは自動追従しない
            // Same formula as PaddedWindowStage.cs:35; duplicated here since production stays untouched,
            // so this guard won't follow if production's own formula changes
            var effectivePadding = Mathf.Max(config.chunkPadding, config.biomeBlendRadius / 2);
            Assert.AreEqual(100, effectivePadding, "forUnitTestの実効paddingが変わった");

            var divisor = Mathf.Max(1, config.boundaryConfig.blurRadiusDivisor);

            // InterpolateWeightsJob(±blendRadius)→Horizontal/VerticalBlurJob(±blendRadius/divisor)の合算読み取り半径
            // Combined read radius: InterpolateWeightsJob(+-blendRadius) then Horizontal/VerticalBlurJob(+-blendRadius/divisor)
            var neededBiomeWeights = config.biomeBlendRadius + config.biomeBlendRadius / divisor;
            Assert.AreEqual(300, neededBiomeWeights);
            Assert.AreEqual(200, neededBiomeWeights - effectivePadding,
                "biomeWeights/winnerBiomeIndexの不足量。v8とは逆にforUnitTestではこちらが不足側");

            // BeachTransitionJobは4半径をそれぞれ別チャネルへ書く。最大値を必要paddingとしないと、
            // beachLandTerrainRadius以外(例: beachSeaTextureRadius)が伸びるドリフトを検知できない
            // BeachTransitionJob writes each of its four radii to a different channel; taking anything less
            // than their max would miss drift where a radius other than beachLandTerrainRadius grows past it
            var neededBeach = Mathf.Max(
                Mathf.Max(config.shoreConfig.beachLandTextureRadius, config.shoreConfig.beachLandTerrainRadius),
                Mathf.Max(config.shoreConfig.beachSeaTextureRadius, config.shoreConfig.beachSeaTerrainRadius));
            Assert.AreEqual(16, neededBeach);
            Assert.LessOrEqual(neededBeach, effectivePadding, "beach系はforUnitTestでは充足しているはず");

            // coastalSmoothFactorはbeachLandTerrainRadiusのみを使う（BeachTransitionJob.cs:65）。
            // 上のneededBeach（4半径の最大）を流用すると別の式を検証したことになるため専用変数に分ける
            // coastalSmoothFactor reads only beachLandTerrainRadius (BeachTransitionJob.cs:65); reusing
            // neededBeach's max here would silently test a different formula, so it gets its own variable
            var landTerrainRadius = config.shoreConfig.beachLandTerrainRadius;
            var neededCoastalHeights = Mathf.Max(landTerrainRadius * 2, landTerrainRadius + 12);
            Assert.AreEqual(22, neededCoastalHeights);
            Assert.LessOrEqual(neededCoastalHeights, effectivePadding, "coastal系heightsはforUnitTestでは充足しているはず");
        }
    }
}
