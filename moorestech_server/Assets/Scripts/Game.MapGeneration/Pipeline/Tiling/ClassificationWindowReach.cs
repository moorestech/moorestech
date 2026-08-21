using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Tiling
{
    /// <summary>
    ///     ClassificationStage が窓の外を読みうる最大画素数。PaddedWindowStage が窓幅を決めるために問い合わせる。
    ///     ステージ本体ではなくタイリング側に置いてあるのは、この値を要るのが窓を切る側だけだから。
    ///     The largest pixel distance ClassificationStage reads outside the window, asked for by PaddedWindowStage to size it.
    ///     It lives on the tiling side rather than in the stage because only the code that cuts the window needs it.
    /// </summary>
    public static class ClassificationWindowReach
    {
        // 海岸系はジョブ側から、重み系は補間とブラーの合算から採る。
        // SmallSeaRemovalJob の連結成分だけは到達が無制限で、この値には含められない（別途裁定・bd moorestech-edd.8）。
        // Shore radii come from the job, weights from interpolation plus blur.
        // SmallSeaRemovalJob's connected components alone have unbounded reach and cannot be folded in (adjudicated separately, bd moorestech-edd.8).
        public static int Pixels(TerrainGenerationConfig config)
        {
            var shore = config.shoreConfig;
            var beachReach = BeachTransitionJob.MaxReachPixels(
                shore.beachLandTextureRadius, shore.beachLandTerrainRadius,
                shore.beachSeaTextureRadius, shore.beachSeaTerrainRadius);

            // InterpolateWeightsJob が ±blendRadius を読み、その結果を H/V ブラーが ±blendRadius/divisor でさらに広げる
            // InterpolateWeightsJob reads +-blendRadius, then the H/V blur widens that result by another +-blendRadius/divisor
            var divisor = Mathf.Max(1, config.boundaryConfig.blurRadiusDivisor);
            var weightsReach = config.biomeBlendRadius + config.biomeBlendRadius / divisor;

            return Mathf.Max(beachReach, weightsReach);
        }
    }
}
