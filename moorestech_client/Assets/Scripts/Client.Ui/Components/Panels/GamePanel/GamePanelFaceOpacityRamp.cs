using UnityEngine;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// 面の縁フェードの頂点 α を決める。4 辺の重みの積を mask とし、面の α に線形に掛ける（Web の mask と同じ。UI は sRGB 空間で合成する。A-180）
    /// Decides face edge-fade vertex alpha: the product of four edge weights is the mask, multiplied linearly into the face alpha (as the Web mask does; the UI blends in sRGB space, A-180)
    /// </summary>
    internal static class GamePanelFaceOpacityRamp
    {
        public static float VertexAlpha(float faceAlpha, float x, float y, float width, float height, float sideFade, float topFade, float bottomFade)
        {
            return faceAlpha * MaskWeightAt(x, y, width, height, sideFade, topFade, bottomFade);
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
    }
}
