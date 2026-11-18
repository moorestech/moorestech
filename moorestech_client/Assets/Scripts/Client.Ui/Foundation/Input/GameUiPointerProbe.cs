using Client.Input;
using Client.Ui.Foundation.Input.Pointer;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 画面座標の下の拾える要素を答える（DI に 1 つ）
    /// Answers the pickable GameUi element under a screen position; registered once in DI and shared by the input installer and the equipment wheel
    /// </summary>
    public sealed class GameUiPointerProbe : IGameUiPointerProbe
    {
        private readonly VisualElement _root;

        public GameUiPointerProbe(VisualElement root)
        {
            _root = root;
        }

        public bool IsPointerOverGameUi(Vector2 screenPosition)
        {
            return PickAt(screenPosition) != null;
        }

        // 画面座標（左下原点）で拾う要素。無ければ null
        // Element picked at a bottom-left screen position; null if none
        public VisualElement PickAt(Vector2 screenPosition)
        {
            // パネルは sRGB 合成の導入が差し替えるので、控えずに拾うたびルートから読む
            // The sRGB composite installer swaps the panel, so read it from the root on every pick instead of keeping it
            return PickAtPanel(UiScreenToPanel.FromBottomLeft(_root.panel, screenPosition));
        }

        // 層コンテナとルートは PickingMode.Ignore なので、Pick が返すのは中身の要素だけ
        // Layer containers and the root are PickingMode.Ignore, so Pick only ever returns content elements
        internal VisualElement PickAtPanel(Vector2 panelPosition)
        {
            return _root.panel.Pick(panelPosition);
        }
    }
}
