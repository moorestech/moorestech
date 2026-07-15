using System;
using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// range+smoothness+noise+curveで連続値フィルタリング。
    /// MicroVerseのFilterSetの各フィルタに相当し、二値判定ではなく0-1の重みを返す。
    /// </summary>
    [Serializable]
    public struct PlacementFilter
    {
        [Label("有効")]
        public bool enabled;
        [Label("範囲")]
        public Vector2 range;
        // 遷移幅: x=min側、y=max側。0なら即カットオフ、>0でsmoothstep遷移
        [Label("遷移幅")]
        public Vector2 smoothness;
        [Label("ノイズ変調")]
        public PlacementNoise noise;
        // AnimationCurveで任意の応答カーブを定義（nullなら線形）
        [Label("カーブ")]
        public AnimationCurve curve;

        /// <summary>
        /// smoothstep遷移付きの0-1フィルタ重みを返す。
        /// noiseValueはフィルタ専用ノイズの出力値（外部でサンプリング済み）。
        /// </summary>
        public float Evaluate(float value, float noiseValue = 0f)
        {
            if (!enabled) return 1f;

            float adjustedValue = value + noiseValue;

            float minEdge = range.x;
            float maxEdge = range.y;
            float sMin = smoothness.x;
            float sMax = smoothness.y;

            // 完全に範囲外なら即棄却
            if (adjustedValue < minEdge - sMin) return 0f;
            if (adjustedValue > maxEdge + sMax) return 0f;

            float weight = 1f;

            // min側: 範囲下限に向かってsmoothstepで減衰
            if (sMin > 0f && adjustedValue < minEdge + sMin)
                weight *= Mathf.SmoothStep(0f, 1f, (adjustedValue - (minEdge - sMin)) / (sMin * 2f));
            else if (adjustedValue < minEdge)
                return 0f;

            // max側: 範囲上限に向かってsmoothstepで減衰
            if (sMax > 0f && adjustedValue > maxEdge - sMax)
                weight *= Mathf.SmoothStep(0f, 1f, ((maxEdge + sMax) - adjustedValue) / (sMax * 2f));
            else if (adjustedValue > maxEdge)
                return 0f;

            // カスタムカーブが設定されていれば追加変調
            if (curve != null && curve.length > 0)
            {
                float t = Mathf.InverseLerp(minEdge, maxEdge, adjustedValue);
                weight *= curve.Evaluate(t);
            }

            return weight;
        }
    }
}
