using UnityEngine;

namespace Client.Ui.State.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの左上の位置を決める。Web の shared/tooltip/tooltipPosition.ts の写し（端で押し戻す）
    /// Decides the cursor tooltip's top-left; a copy of Web shared/tooltip/tooltipPosition.ts (pushes back at the edges)
    /// </summary>
    public static class CursorTooltipPlacement
    {
        // web: moorestech_web/webui/src/shared/tooltip/tooltipPosition.ts の OFFSET。実画面 px で効く
        // web: OFFSET in moorestech_web/webui/src/shared/tooltip/tooltipPosition.ts; applies in screen pixels
        public const float Offset = 12f;

        public static Vector2 Clamp(Vector2 pointer, Vector2 size, Vector2 viewport)
        {
            return new Vector2(ClampAxis(pointer.x, size.x, viewport.x), ClampAxis(pointer.y, size.y, viewport.y));
        }

        public static Vector2 PlaceInPanel(Vector2 pointerPanel, Vector2 sizePanel, Vector2 viewportPanel, float screenPixelsPerPanelPixel)
        {
            // Web は実画面 px で計算するので、パネル単位を実画面 px へ直して計算し戻す
            // Web computes in screen pixels, so convert panel units to screen pixels and back
            var scale = screenPixelsPerPanelPixel;
            return Clamp(pointerPanel * scale, sizePanel * scale, viewportPanel * scale) / scale;
        }

        // Web と同じく max を外に置き、収まらない大きさでは左上の余白を優先する
        // Like Web, max is outermost so the top-left margin wins when the tooltip cannot fit
        private static float ClampAxis(float pointer, float size, float viewport)
        {
            return Mathf.Max(Offset, Mathf.Min(pointer + Offset, viewport - size - Offset));
        }
    }
}
