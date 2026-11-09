using UnityEngine;

namespace Client.Ui.Foundation.Theme
{
    /// <summary>
    /// ガウスでぼかした縁の被覆率の唯一の式
    /// The only formula for the coverage of a Gaussian-blurred edge
    /// </summary>
    /// <remarks>
    /// 直線の縁をぼかした被覆率は標準正規分布の累積分布 Φ になる。σ の決め方は使い手が持つ（box-shadow は blur/2、drop-shadow の第 3 値は σ そのもの）。
    /// erf は Abramowitz-Stegun 7.1.26（誤差 1.5e-7）で、8 bit の α には十分。矩形・円の影（ダイアログ・クロスヘア）と矢印の影が使う。
    /// Coverage across a blurred straight edge is the standard normal CDF Phi. Users own how sigma is chosen (box-shadow uses blur/2; the third drop-shadow value is sigma itself).
    /// erf uses Abramowitz-Stegun 7.1.26 (error 1.5e-7), ample for an 8-bit alpha. Rect and circle shadows (dialog, crosshair) and the arrow shadow use it.
    /// </remarks>
    internal static class GaussianEdgeCoverage
    {
        // 縁から外向きに距離 d の点の被覆率 Φ(-d/σ)（内側は d が負）
        // Coverage Phi(-d/sigma) at outward distance d from the edge (negative d is inside)
        public static float At(float signedDistance, float sigma)
        {
            return StandardNormalCdf(-signedDistance / sigma);
        }

        // 区間 [low, high] をぼかした帯の点 position での被覆率 Φ((p-low)/σ)-Φ((p-high)/σ)
        // Coverage at position of the band [low, high] blurred by sigma: Phi((p-low)/sigma)-Phi((p-high)/sigma)
        public static float BandCoverage(float position, float low, float high, float sigma)
        {
            return Mathf.Max(0f, StandardNormalCdf((position - low) / sigma) - StandardNormalCdf((position - high) / sigma));
        }

        public static float StandardNormalCdf(float z)
        {
            var x = Mathf.Abs(z) / Mathf.Sqrt(2f);
            var t = 1f / (1f + 0.3275911f * x);
            var polynomial = t * (0.254829592f + t * (-0.284496736f + t * (1.421413741f + t * (-1.453152027f + t * 1.061405429f))));
            var erf = 1f - polynomial * Mathf.Exp(-x * x);
            return 0f <= z ? 0.5f * (1f + erf) : 0.5f * (1f - erf);
        }
    }
}
