using UnityEngine;

namespace Client.Ui.Foundation.Motion.Tween
{
    public enum UiEasing
    {
        Linear,
        EaseOutCubic,
        EaseInOutSine,
        // Web の CSS 既定の ease-out・ease-in（cubic-bezier(0,0,.58,1)・(.42,0,1,1)）。EaseOutCubic とは値が違う
        // Web CSS default ease-out and ease-in (cubic-bezier(0,0,.58,1) and (.42,0,1,1)); values differ from EaseOutCubic
        CssEaseOut,
        CssEaseIn,
    }

    /// <summary>
    /// 0〜1 の進み具合を曲線で写す
    /// Maps a 0..1 progress through an easing curve
    /// </summary>
    public static class UiEasingFunctions
    {
        public static float Evaluate(UiEasing easing, float t)
        {
            switch (easing)
            {
                case UiEasing.Linear:
                    return t;
                case UiEasing.EaseOutCubic:
                    var remaining = 1f - t;
                    return 1f - remaining * remaining * remaining;
                case UiEasing.EaseInOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case UiEasing.CssEaseOut:
                    return CssCubicBezier(0f, 0f, 0.58f, 1f, t);
                case UiEasing.CssEaseIn:
                    return CssCubicBezier(0.42f, 0f, 1f, 1f, t);
                default:
                    Debug.LogError($"[UiEasingFunctions] unknown easing {easing}; falling back to linear");
                    return t;
            }
        }

        private const int CssBezierBisectionSteps = 40;

        // x(s)=t を二分法で解いて y(s) を返す（x は s について単調）。端は丸めずに 0 と 1 を返す
        // Solve x(s)=t by bisection and return y(s) (x is monotonic in s); the ends return exactly 0 and 1
        private static float CssCubicBezier(float x1, float y1, float x2, float y2, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            var low = 0f;
            var high = 1f;
            for (var i = 0; i < CssBezierBisectionSteps; i++)
            {
                var middle = (low + high) * 0.5f;
                if (Bezier(middle, x1, x2) < t) low = middle;
                else high = middle;
            }
            return Bezier((low + high) * 0.5f, y1, y2);
        }

        private static float Bezier(float s, float p1, float p2)
        {
            var u = 1f - s;
            return 3f * u * u * s * p1 + 3f * u * s * s * p2 + s * s * s;
        }
    }
}
