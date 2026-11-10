using Client.Input;
using Client.Ui.Foundation.Input.Pointer;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 画面座標の下の GameUi の拾える要素を答える
    /// Answers the pickable GameUi element under a screen position
    /// </summary>
    public sealed class GameUiPointerProbe : IGameUiPointerProbe
    {
        private readonly IPanel _panel;

        public GameUiPointerProbe(IPanel panel)
        {
            _panel = panel;
        }

        public bool IsPointerOverGameUi(Vector2 screenPosition)
        {
            return PickAt(screenPosition) != null;
        }

        // 画面座標（左下原点）で拾う要素。無ければ null
        // Element picked at a bottom-left screen position; null if none
        public VisualElement PickAt(Vector2 screenPosition)
        {
            return PickAtPanel(UiScreenToPanel.FromBottomLeft(_panel, screenPosition));
        }

        // 層コンテナとルートは PickingMode.Ignore なので、Pick が返すのは中身の要素だけ
        // Layer containers and the root are PickingMode.Ignore, so Pick only ever returns content elements
        internal VisualElement PickAtPanel(Vector2 panelPosition)
        {
            return _panel.Pick(panelPosition);
        }
    }
}
