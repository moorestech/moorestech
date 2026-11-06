using UnityEngine;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// 面の縁フェードの頂点 α を決める。4 辺の重みの積を mask とし、Web の sRGB 合成と同じ濃さの並びになるよう Linear 合成用に補正する
    /// Decides face edge-fade vertex alpha: the product of four edge weights is the mask, corrected for Linear blending to match the Web sRGB run of shades
    /// </summary>
    /// <remarks>
    /// Web は mask の重み m を sRGB 合成の α に線形に掛ける。Linear 合成で α を線形に補間すると、暗い面が明るい世界の上で
    /// 縁から遅れて濃くなる（スキット上端の 50% 点が約 9px 内側へずれる）。面のトークンは S7 の式 1-(1-a)^1.85 で補正済みなので、
    /// それを逆に解いて Web の α を戻し、重みを掛けてから同じ式で補正し直す。重み 1 ではトークンの α そのものに戻る。
    /// この逆算は「面トークンの α が S7 の式（指数 1.85。tools/uitk/webTokens/translucency.mjs と同じ）で補正済み」であることに依る。面トークンの α や補正の指数を変えたら
    /// 縁の中間の濃さがずれるので、スキット上端の 50% 点を Web と並べて再実測する（docs/uitk/TODO.md の S7・C2-a の縁フェード）。
    /// The Web multiplies the mask weight m linearly into the sRGB-blend alpha. Interpolating alpha linearly under Linear
    /// blending makes a dark face over a bright world darken late (the skit top 50% point drifts about 9px inward). The face
    /// token is already corrected by the S7 formula 1-(1-a)^1.85, so invert it to recover the Web alpha, apply the weight,
    /// and correct again with the same formula; a weight of 1 returns the token alpha unchanged.
    /// This inversion relies on the face token alpha being S7-corrected with exponent 1.85 (the same as tools/uitk/webTokens/translucency.mjs). If a face token alpha or the exponent
    /// changes, the mid-fade shades drift, so re-measure the skit top 50% point against the Web (S7 and the C2-a edge fade in docs/uitk/TODO.md).
    /// </remarks>
    internal static class GamePanelFaceOpacityRamp
    {
        // S7 の α 補正の指数（暗い色の半透明）。生成器 translucency.mjs の darkExponent と同じ値にする
        // Exponent of the S7 alpha correction for dark translucency; keep it equal to darkExponent in the generator translucency.mjs
        // 1 つの指数では地の明るさごとの差が残る（look-exception: LX-43）
        // One exponent leaves a per-backdrop residual (look-exception: LX-43)
        private const float LinearBlendExponent = 1.85f;

        public static float VertexAlpha(float faceAlpha, float x, float y, float width, float height, float sideFade, float topFade, float bottomFade)
        {
            return OpacityAt(faceAlpha, MaskWeightAt(x, y, width, height, sideFade, topFade, bottomFade));
        }

        public static float MaskWeightAt(float x, float y, float width, float height, float sideFade, float topFade, float bottomFade)
        {
            // 4 辺の重みの積（Web の mask-composite: intersect と同じく積を取る）
            // Product of the four edge weights (the same product as the Web mask-composite: intersect)
            return Ramp(y, topFade) * Ramp(height - y, bottomFade) * Ramp(x, sideFade) * Ramp(width - x, sideFade);

            #region Internal

            // 縁からの距離をフェード幅で割った重み。幅 0 の辺は常に 1（mask を掛けない辺。0 で割らない）
            // Distance from the edge over the fade span; a zero-span side is always 1 (an unmasked side, never divided by zero)
            float Ramp(float distance, float fade)
            {
                return fade <= 0f ? 1f : Mathf.Clamp01(distance / fade);
            }

            #endregion
        }

        public static float OpacityAt(float faceAlpha, float maskWeight)
        {
            var webAlpha = 1f - Mathf.Pow(1f - faceAlpha, 1f / LinearBlendExponent);
            return 1f - Mathf.Pow(1f - webAlpha * maskWeight, LinearBlendExponent);
        }
    }
}
