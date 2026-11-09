using UnityEngine;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// ゲージの値を 0..1 へ丸める（Web の shared/clamp01.ts。NaN は 0）。Gauges の 4 部品が共有する
    /// Clamps a gauge value to 0..1 (the Web shared/clamp01.ts; NaN is 0), shared by the four Gauges parts
    /// </summary>
    internal static class GaugeRatio
    {
        public static float Clamp01(float ratio)
        {
            return float.IsNaN(ratio) ? 0f : Mathf.Clamp01(ratio);
        }
    }
}
