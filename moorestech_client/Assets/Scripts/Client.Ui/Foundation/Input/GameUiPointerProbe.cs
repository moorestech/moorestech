using Client.Input;
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
            // Unity のスクリーン座標は左下原点、パネル換算は左上原点のスクリーン座標を受ける
            // Unity screen space is bottom-left origin while the panel conversion expects top-left screen coordinates
            var topLeftScreenPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var panelPosition = RuntimePanelUtils.ScreenToPanel(_panel, topLeftScreenPosition);
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
