using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // range+smoothness+noise+curve で連続値フィルタリングし 0-1 の重みを返す。
    // Continuous-value filter over range+smoothness+noise+curve returning a 0-1 weight.
    public struct PlacementFilter
    {
        public bool enabled;
        public Vector2 range;
        public Vector2 smoothness;
        public PlacementNoise noise;
        public AnimationCurve curve;

        public float Evaluate(float value, float noiseValue)
        {
            if (!enabled) return 1f;

            float adjustedValue = value + noiseValue;

            float minEdge = range.x;
            float maxEdge = range.y;
            float sMin = smoothness.x;
            float sMax = smoothness.y;

            if (adjustedValue < minEdge - sMin) return 0f;
            if (adjustedValue > maxEdge + sMax) return 0f;

            float weight = 1f;

            if (sMin > 0f && adjustedValue < minEdge + sMin)
                weight *= Mathf.SmoothStep(0f, 1f, (adjustedValue - (minEdge - sMin)) / (sMin * 2f));
            else if (adjustedValue < minEdge)
                return 0f;

            if (sMax > 0f && adjustedValue > maxEdge - sMax)
                weight *= Mathf.SmoothStep(0f, 1f, ((maxEdge + sMax) - adjustedValue) / (sMax * 2f));
            else if (adjustedValue > maxEdge)
                return 0f;

            if (curve != null && curve.length > 0)
            {
                float t = Mathf.InverseLerp(minEdge, maxEdge, adjustedValue);
                weight *= curve.Evaluate(t);
            }

            return weight;
        }
    }
}
