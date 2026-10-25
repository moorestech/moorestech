using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkStage0Probe
{
    // パネル座標と画面座標（左下原点のピクセル）の往復。公開APIのScreenToPanelから逆変換を組む
    // Round-trips panel coordinates and screen pixels (bottom-left origin); the inverse is derived from the public ScreenToPanel
    public static class ProbeCoordinates
    {
        public static Vector2 ScreenToPanel(IPanel panel, Vector2 screenBottomLeft)
        {
            // ScreenToPanelは左上原点の画面座標を受けるので、Y を反転してから渡す
            // ScreenToPanel takes top-left-origin screen coordinates, so flip Y before passing it
            var topLeft = new Vector2(screenBottomLeft.x, Screen.height - screenBottomLeft.y);
            return RuntimePanelUtils.ScreenToPanel(panel, topLeft);
        }

        public static Vector2 PanelToScreen(IPanel panel, Vector2 panelPosition)
        {
            // 2点の写像からスケールと原点ずれを求め、線形の逆写像で戻す
            // Derive scale and offset from two mapped points and invert the linear map
            var origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            var unit = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(1000f, 1000f));
            var scale = (unit - origin) / 1000f;
            var topLeft = new Vector2((panelPosition.x - origin.x) / scale.x, (panelPosition.y - origin.y) / scale.y);
            return new Vector2(topLeft.x, Screen.height - topLeft.y);
        }
    }
}
