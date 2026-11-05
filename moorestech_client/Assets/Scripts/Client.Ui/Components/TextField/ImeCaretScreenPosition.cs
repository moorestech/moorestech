using UnityEngine;

namespace Client.Ui.Components.TextField
{
    /// <summary>
    /// キャレットのパネル座標を、IME の候補窓位置（Input.compositionCursorPos）に書くスクリーンのピクセル（左上原点）へ換算する
    /// Converts a caret's panel point into the screen pixels (top-left origin) written to Input.compositionCursorPos for the IME candidate window
    /// </summary>
    public static class ImeCaretScreenPosition
    {
        public static Vector2 FromPanel(Vector2 panelPoint, Vector2 panelSize, Vector2 screenSize)
        {
            // RuntimePanelUtils.ScreenToPanel の逆で、軸ごとにスクリーン/パネルの比を掛ける（y は反転しない）
            // Inverse of RuntimePanelUtils.ScreenToPanel: scale each axis by screen/panel, without flipping y
            return new Vector2(
                panelPoint.x * screenSize.x / panelSize.x,
                panelPoint.y * screenSize.y / panelSize.y);
        }
    }
}
