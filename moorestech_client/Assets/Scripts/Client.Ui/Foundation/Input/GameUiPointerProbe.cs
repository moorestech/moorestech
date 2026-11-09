using Client.Input;
using Client.Ui.Foundation.Input.Pointer;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// スクリーン座標の下に GameUi の拾える要素があるかを panel.Pick で答える
    /// Answers whether a pickable GameUi element lies under a screen position, using panel.Pick
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
            var panelPosition = UiScreenToPanel.FromBottomLeft(_panel, screenPosition);
            return IsPickableAt(panelPosition);
        }

        // 層コンテナとルートは PickingMode.Ignore なので、Pick が返すのは中身の要素だけ
        // Layer containers and the root are PickingMode.Ignore, so Pick only ever returns content elements
        internal bool IsPickableAt(Vector2 panelPosition)
        {
            return _panel.Pick(panelPosition) != null;
        }
    }
}
