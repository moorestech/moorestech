using UnityEngine;

namespace Client.Ui.Foundation.Motion.Tween
{
    public enum UiEasing
    {
        Linear,
        EaseOutCubic,
        EaseInOutSine,
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
                default:
                    Debug.LogError($"[UiEasingFunctions] unknown easing {easing}; falling back to linear");
                    return t;
            }
        }
    }
}
