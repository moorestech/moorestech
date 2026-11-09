using Client.Ui.Foundation.Motion.Inertia;
using UnityEngine;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// パンの慣性の定数と速度の計算（速度は px/ms）。web: moorestech_web/webui/src/shared/treeView/viewport/viewport.ts
    /// Pan inertia constants and velocity math (px/ms); web: moorestech_web/webui/src/shared/treeView/viewport/viewport.ts
    /// </summary>
    /// <remarks>
    /// τ=325ms は iOS 系のスクロールの減衰の標準値。上限 3px/ms で最大の滑走は 3×325≒975px（Web のコメントのまま）
    /// τ=325 ms is the standard iOS-style decay; the 3 px/ms cap bounds the glide at 3×325≒975 px (as the Web comment says)
    /// </remarks>
    internal static class NodeGraphPanVelocity
    {
        public const float FrictionTauMs = 325f;
        public const float MinFlingSpeed = 0.15f;
        public const float StopSpeed = 0.01f;
        public const float MaxFlingSpeed = 3f;
        public const long SampleMaxGapMs = 80;
        public const long ReleaseStallMs = 80;
        public const float SmoothingTauMs = 50f;

        public static Vector2 Blend(Vector2 velocity, Vector2 delta, float dtMs)
        {
            // 直近の標本で速度を平滑し、急停止の外れ値を均す
            // Smooth the velocity with the latest sample to even out sudden-stop outliers
            var alpha = 1f - Mathf.Exp(-dtMs / SmoothingTauMs);
            return velocity + (delta / dtMs - velocity) * alpha;
        }

        public static Vector2 Clamp(Vector2 velocity)
        {
            var speed = velocity.magnitude;
            if (speed <= MaxFlingSpeed) return velocity;
            return velocity * (MaxFlingSpeed / speed);
        }

        public static UiInertiaAnimation CreateGlide(float speed, IUiInertiaStep step)
        {
            // 減衰率は 1/τ。両軸とも同じ τ なので向きは変わらず、速さ 1 本の閉じた式で写せる（D-6）
            // The decay rate is 1/τ; both axes share τ, so the direction never changes and one closed-form speed suffices (D-6)
            // look-exception: LX-87
            return new UiInertiaAnimation(speed, 1f / FrictionTauMs, StopSpeed, step);
        }
    }
}
