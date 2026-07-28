using Game.MapGeneration.Pipeline.Jobs;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Detail
{
    /// <summary>
    ///     傾斜・曲率・方位・距離に共通のフィルタ。MapMaking DetailFilter の移植
    ///     Filter shared by slope, curvature, azimuth, and distance; ported from MapMaking's DetailFilter
    /// </summary>
    public class DetailFilter
    {
        public enum Mode
        {
            Simple,
            Curve
        }

        public bool enabled;
        public Mode mode;

        // フィルタ結果に掛ける重み。0にすると有効でも効果が出ない
        // Weight applied to the filter result; 0 nullifies the filter even while enabled
        public float weight;

        // Simpleモード: この範囲内で1、範囲外はsmoothnessで滑らかに0へ落ちる
        // Simple mode: 1 inside this range, falling smoothly to 0 outside it over smoothness
        public Vector2 range;
        public Vector2 smoothness;

        // フィルタ自体をノイズで変調し、境界線をランダムに揺らす
        // Modulates the filter itself with noise so its boundary wavers
        public DetailNoiseLayer noise;

        // Curveモード: 入力値から出力値への自由なマッピング
        // Curve mode: a free-form mapping from input value to output value
        public AnimationCurve curve;

        public float Evaluate(float value, float worldX, float worldZ, Vector2[] noiseOffsets)
        {
            if (!enabled) return 1f;

            // Simpleは台形フィルタ、CurveはAnimationCurveの応答をそのまま使う
            // Simple uses the trapezoid filter; Curve takes the AnimationCurve response as-is
            var result = mode == Mode.Curve
                ? curve.Evaluate(value)
                : BurstTerrainMath.FilterRange(value, range.x, range.y, smoothness.x, smoothness.y);

            if (noise.IsActive) result *= noise.Sample(worldX, worldZ, noiseOffsets);

            return Mathf.Clamp01(result * weight);
        }
    }
}
