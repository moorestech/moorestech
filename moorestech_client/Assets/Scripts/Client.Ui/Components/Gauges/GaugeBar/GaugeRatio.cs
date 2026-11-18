using UnityEngine;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 値を 0..1 へ丸める（NaN は 0）
    /// Clamps a value to 0..1 (NaN is 0)
    /// </summary>
    internal static class GaugeRatio
    {
        public static float Clamp01(float ratio)
        {
            return float.IsNaN(ratio) ? 0f : Mathf.Clamp01(ratio);
        }
    }
}
