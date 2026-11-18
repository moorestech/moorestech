using UnityEngine;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// ツールチップの指す点を実画面 px（左下原点）で決める
    /// Decides the point the tooltip anchors to, in screen px (bottom-left origin)
    /// </summary>
    internal static class CursorTooltipScreenAnchor
    {
        internal static Vector2 AnchorOnScreen(bool cursorLocked, Vector2 latestPointerOnScreen, Vector2 screenCenter)
        {
            // ロック中はポインタが凍るので、クロスヘアと同じ画面中央を指す
            // The pointer freezes while locked, so point at the screen centre like the crosshair
            return cursorLocked ? screenCenter : latestPointerOnScreen;
        }
    }
}
