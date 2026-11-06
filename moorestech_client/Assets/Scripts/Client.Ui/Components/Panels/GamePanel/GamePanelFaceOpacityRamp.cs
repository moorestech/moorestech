using UnityEngine;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// 面の縁フェードの途中の α を、Web の sRGB 合成と同じ濃さの並びになるよう Linear 合成用に補正する
    /// Corrects the alpha inside a face edge fade for Linear blending so its run of shades matches the Web sRGB blend
    /// </summary>
    /// <remarks>
    /// Web は mask の重み m を sRGB 合成の α に線形に掛ける。Linear 合成で α を線形に補間すると、暗い面が明るい世界の上で
    /// 縁から遅れて濃くなる（スキット上端の 50% 点が約 9px 内側へずれる）。面のトークンは S7 の式 1-(1-a)^2.2 で補正済みなので、
    /// それを逆に解いて Web の α を戻し、重みを掛けてから同じ式で補正し直す。重み 1 ではトークンの α そのものに戻る。
    /// The Web multiplies the mask weight m linearly into the sRGB-blend alpha. Interpolating alpha linearly under Linear
    /// blending makes a dark face over a bright world darken late (the skit top 50% point drifts about 9px inward). The face
    /// token is already corrected by the S7 formula 1-(1-a)^2.2, so invert it to recover the Web alpha, apply the weight,
    /// and correct again with the same formula; a weight of 1 returns the token alpha unchanged.
    /// </remarks>
    internal static class GamePanelFaceOpacityRamp
    {
        // S7 の α 補正の指数（暗い色の半透明）。docs/uitk/TODO.md の S7
        // Exponent of the S7 alpha correction for dark translucency; see S7 in docs/uitk/TODO.md
        private const float LinearBlendExponent = 2.2f;

        public static float OpacityAt(float faceAlpha, float maskWeight)
        {
            var webAlpha = 1f - Mathf.Pow(1f - faceAlpha, 1f / LinearBlendExponent);
            return 1f - Mathf.Pow(1f - webAlpha * maskWeight, LinearBlendExponent);
        }
    }
}
