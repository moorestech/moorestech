using UnityEngine;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// ツールチップの指す点を、パネル換算が受ける左上原点の画面 px で決める
    /// Decides the point the tooltip anchors to, in top-left-origin screen px for the panel conversion
    /// </summary>
    internal static class CursorTooltipScreenAnchor
    {
        internal static Vector2 TopLeftScreenPosition(bool cursorLocked, Vector2 latestPointerOnScreen, Vector2 screenCenter, float screenHeight)
        {
            // ロック中はポインタが凍るので、クロスヘアと同じ画面中央を指す
            // The pointer freezes while locked, so point at the screen centre like the crosshair
            var bottomLeftScreenPosition = cursorLocked ? screenCenter : latestPointerOnScreen;

            // Unity の画面 px は左下原点、パネル換算は左上原点を受ける
            // Unity screen px is bottom-left origin while the panel conversion takes top-left
            return new Vector2(bottomLeftScreenPosition.x, screenHeight - bottomLeftScreenPosition.y);
        }
    }
}
