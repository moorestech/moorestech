using UnityEngine;

namespace Client.Ui.Foundation.Theme
{
    /// <summary>
    /// S7 の式で補正済みの α に重み（縁の mask・影の被覆率）を掛けた Linear 合成用の α を返す
    /// Returns the Linear-blend alpha for an S7-corrected alpha scaled by a weight (edge mask or shadow coverage)
    /// </summary>
    /// <remarks>
    /// Web は重み m を sRGB 合成の α に線形に掛ける。Linear 合成で α を線形に掛けると、暗い色が明るい地の上で縁から遅れて濃くなる。
    /// そこで補正済みの α を S7 の式 1-(1-a)^1.85 の逆で Web の α に戻し、重みを掛けてから同じ式で補正し直す。重み 1 では元の α に戻る。
    /// 面・ヴィネット・クロスヘアの影・ダイアログの影・矢印の影が使う。トークンの α か指数を変えたら、使い手ごとの縁の濃さを Web と並べて再実測する。
    /// The Web multiplies the weight m linearly into the sRGB-blend alpha; doing so under Linear blending makes a dark color over a bright
    /// backdrop darken late from the edge. So invert the S7 formula 1-(1-a)^1.85 to recover the Web alpha, apply the weight, and correct
    /// again with the same formula; a weight of 1 returns the original alpha. Faces, the vignette, the crosshair shadow, dialog shadows and the arrow shadow
    /// use it; if a token alpha or the exponent changes, re-measure each user's edge shades against the Web.
    /// </remarks>
    internal static class LinearBlendOpacity
    {
        // S7 の α 補正の指数（暗い色の半透明）。生成器 translucency.mjs の darkExponent と同じ値にする
        // Exponent of the S7 alpha correction for dark translucency; keep it equal to darkExponent in the generator translucency.mjs
        // 1 つの指数では地の明るさごとの差が残る（look-exception: LX-43）
        // One exponent leaves a per-backdrop residual (look-exception: LX-43)
        private const float LinearBlendExponent = 1.85f;

        public static float OpacityAt(float correctedAlpha, float weight)
        {
            var webAlpha = 1f - Mathf.Pow(1f - correctedAlpha, 1f / LinearBlendExponent);
            return 1f - Mathf.Pow(1f - webAlpha * weight, LinearBlendExponent);
        }
    }
}
