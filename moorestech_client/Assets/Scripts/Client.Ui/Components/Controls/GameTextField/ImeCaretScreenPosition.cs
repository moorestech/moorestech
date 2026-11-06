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
    /// パネル座標→スクリーンpxへ換算
    /// Convert panel point to screen pixels
    /// </summary>
    public static class ImeCaretScreenPosition
    {
        public static ImeCursorOrigin PlatformOrigin =>
            Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer ? ImeCursorOrigin.BottomLeft : ImeCursorOrigin.TopLeft;

        public static Vector2 FromPanel(Vector2 panelPoint, Vector2 panelSize, Vector2 screenSize, ImeCursorOrigin origin)
        {
            // 軸ごとに画面/パネル比を掛ける
            // Scale each axis by screen/panel
            var x = panelPoint.x * screenSize.x / panelSize.x;
            var yFromTop = panelPoint.y * screenSize.y / panelSize.y;
            return new Vector2(x, origin == ImeCursorOrigin.BottomLeft ? screenSize.y - yFromTop : yFromTop);
        }
    }
}
