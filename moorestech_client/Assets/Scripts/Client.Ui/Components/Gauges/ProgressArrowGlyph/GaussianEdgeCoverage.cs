using UnityEngine;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// ガウスでぼかした形の、縁から符号付き距離（外が正）の点の被覆率 Φ(-d/σ)
    /// Coverage Φ(-d/σ) of a Gaussian-blurred shape at a signed distance from its edge (outside positive)
    /// </summary>
    /// <remarks>
    /// CSS の drop-shadow の第 3 値は標準偏差 σ そのもの（Filter Effects 1 の等価 SVG は feGaussianBlur stdDeviation=radius。box-shadow の σ=ぼかし/2 とは違う）。直線の縁の被覆率はその累積分布になる。
    /// erf は Abramowitz-Stegun 7.1.26（誤差 1.5e-7）で、8 bit の α には十分。
    /// The third CSS drop-shadow value is σ itself (the Filter Effects 1 SVG equivalent is feGaussianBlur stdDeviation=radius, unlike box-shadow's σ = blur/2); coverage across a straight edge is its cumulative distribution.
    /// erf uses Abramowitz-Stegun 7.1.26 (error 1.5e-7), ample for an 8-bit alpha.
    /// </remarks>
    internal static class GaussianEdgeCoverage
    {
        public static float At(float signedDistance, float sigma)
        {
            var z = signedDistance / (sigma * Mathf.Sqrt(2f));
            return 0.5f * (1f - Erf(z));

            #region Internal

            float Erf(float x)
            {
                var magnitude = Mathf.Abs(x);
                var t = 1f / (1f + 0.3275911f * magnitude);
                var polynomial = t * (0.254829592f + t * (-0.284496736f + t * (1.421413741f + t * (-1.453152027f + t * 1.061405429f))));
                var positive = 1f - polynomial * Mathf.Exp(-magnitude * magnitude);
                return x < 0f ? -positive : positive;
            }

            #endregion
        }
    }
}
