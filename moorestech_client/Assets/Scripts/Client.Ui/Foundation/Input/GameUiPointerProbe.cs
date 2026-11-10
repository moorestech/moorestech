using Client.Input;
using Client.Ui.Foundation.Input.Pointer;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// スクリーン座標の下にある GameUi の拾える要素を panel.Pick で答える
    /// Answers which pickable GameUi element lies under a screen position, using panel.Pick
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

        // 画面座標（左下原点）の下で拾われる要素。無ければ null（ワールド側）
        // The element picked under a bottom-left-origin screen position; null means the world
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
