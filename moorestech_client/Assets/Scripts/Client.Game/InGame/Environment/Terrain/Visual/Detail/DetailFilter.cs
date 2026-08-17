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

        // ノイズで境界を揺らす
        // Distort the border with noise
        public DetailNoiseLayer noise;

        // Curve: 入力を自由変換
        // Curve: freely remap input
        public AnimationCurve curve;

        // このフィルタが応答を変え得る入力の上限。モードごとに読む場所が違うのでフィルタ自身が答える
        // The largest input this filter still reacts to; each mode reads a different field, so the filter itself answers
        public float RequiredInputRange
        {
            get
            {
                if (!enabled) return 0f;

                // Simpleは上限側の減衰の裾まで、Curveは最後のキーの時刻までが応答範囲
                // Simple reaches to the end of the upper falloff tail, Curve to the time of its last key
                if (mode != Mode.Curve) return range.y + smoothness.y;

                // キーの無いカーブは全域0を返すので、応答範囲も0が正確な答えになる
                // A keyless curve evaluates to 0 everywhere, so 0 is the exact range rather than a fallback
                var keys = curve.keys;
                return keys.Length == 0 ? 0f : keys[keys.Length - 1].time;
            }
        }

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
