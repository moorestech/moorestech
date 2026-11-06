using UnityEngine;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// Input.compositionCursorPos の y の原点。Windows は左上、Mac は左下で読まれる（S9 と段階 1 の実機で、Mac だけ候補窓が上下反転した）
    /// Y origin of Input.compositionCursorPos: Windows reads it top-left, Mac bottom-left (on hardware in S9 and stage 1 only Mac mirrored the candidate window vertically)
    /// </summary>
    public enum ImeCursorOrigin
    {
        TopLeft,
        BottomLeft,
    }

    /// <summary>
    /// キャレットのパネル座標を、IME の候補窓位置（Input.compositionCursorPos）に書くスクリーンのピクセルへ換算する
    /// Converts a caret's panel point into the screen pixels written to Input.compositionCursorPos for the IME candidate window
    /// </summary>
    public static class ImeCaretScreenPosition
    {
        public static ImeCursorOrigin PlatformOrigin =>
            Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer ? ImeCursorOrigin.BottomLeft : ImeCursorOrigin.TopLeft;

        public static Vector2 FromPanel(Vector2 panelPoint, Vector2 panelSize, Vector2 screenSize, ImeCursorOrigin origin)
        {
            // RuntimePanelUtils.ScreenToPanel の逆で、軸ごとにスクリーン/パネルの比を掛ける（パネルは左上原点）
            // Inverse of RuntimePanelUtils.ScreenToPanel: scale each axis by screen/panel (the panel is top-left origin)
            var x = panelPoint.x * screenSize.x / panelSize.x;
            var yFromTop = panelPoint.y * screenSize.y / panelSize.y;
            return new Vector2(x, origin == ImeCursorOrigin.BottomLeft ? screenSize.y - yFromTop : yFromTop);
        }
    }
}
