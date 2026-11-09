using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input.Pointer
{
    /// <summary>
    /// 実画面 px（左下原点）からパネル座標への換算の唯一の式
    /// The only formula converting screen px (bottom-left origin) to panel coordinates
    /// </summary>
    internal static class UiScreenToPanel
    {
        public static Vector2 FromBottomLeft(IPanel panel, Vector2 bottomLeftScreenPosition)
        {
            // Unity の画面 px は左下原点、RuntimePanelUtils は左上原点の画面 px を受ける
            // Unity screen px is bottom-left origin while RuntimePanelUtils takes top-left screen px
            var topLeftScreenPosition = new Vector2(bottomLeftScreenPosition.x, Screen.height - bottomLeftScreenPosition.y);
            return RuntimePanelUtils.ScreenToPanel(panel, topLeftScreenPosition);
        }
    }
}
