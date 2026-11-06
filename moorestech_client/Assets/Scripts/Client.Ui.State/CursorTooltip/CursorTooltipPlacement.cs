using UnityEngine;

namespace Client.Ui.State.CursorTooltip
{
    /// <summary>
    /// ツールチップの左上位置を決める（端で押し戻す）
    /// Decides the tooltip top-left, pushed back at edges
    /// </summary>
    public static class CursorTooltipPlacement
    {
        // web: moorestech_web/webui/src/shared/tooltip/tooltipPosition.ts の OFFSET。実画面 px で効く
        // web: OFFSET in moorestech_web/webui/src/shared/tooltip/tooltipPosition.ts; applies in screen pixels
        internal const float Offset = 12f;

        internal static Vector2 Clamp(Vector2 pointer, Vector2 size, Vector2 viewport)
        {
            return new Vector2(ClampAxis(pointer.x, size.x, viewport.x), ClampAxis(pointer.y, size.y, viewport.y));

            #region Internal

            // max を外に置き、収まらない時は左上優先
            // max outermost; top-left wins when it cannot fit
            static float ClampAxis(float pointerAxis, float sizeAxis, float viewportAxis)
            {
                return Mathf.Max(Offset, Mathf.Min(pointerAxis + Offset, viewportAxis - sizeAxis - Offset));
            }

            #endregion
        }

        public static Vector2 PlaceInPanel(Vector2 pointerPanel, Vector2 sizePanel, Vector2 viewportPanel, float screenPixelsPerPanelPixel)
        {
            // パネル単位を実画面 px へ直して計算し戻す
            // Convert panel units to screen px, compute, convert back
            var scale = screenPixelsPerPanelPixel;
            return Clamp(pointerPanel * scale, sizePanel * scale, viewportPanel * scale) / scale;
        }
    }
}
