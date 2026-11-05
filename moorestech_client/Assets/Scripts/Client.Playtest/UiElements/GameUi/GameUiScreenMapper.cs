using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Playtest.UiElements.GameUi
{
    /// <summary>
    /// 本番の画面寸法とルートの worldBound から倍率を出し、パネル矩形をスクリーン矩形へ写す
    /// Derives the scale from the live screen size and the root worldBound, then maps panel rects to screen rects
    /// </summary>
    internal sealed class GameUiScreenMapper : IGameUiScreenMapper
    {
        private readonly VisualElement _root;

        public GameUiScreenMapper(VisualElement root)
        {
            _root = root;
        }

        public Rect PanelToScreen(Rect panelRect)
        {
            return Map(panelRect, _root.worldBound.size, new Vector2(Screen.width, Screen.height));
        }

        // 軸ごとにスクリーン/パネルの比を掛け、y を反転して左下原点へ直す
        // Scale each axis by screen/panel and flip y into the bottom-left origin
        internal static Rect Map(Rect panelRect, Vector2 panelSize, Vector2 screenSize)
        {
            var scaleX = screenSize.x / panelSize.x;
            var scaleY = screenSize.y / panelSize.y;
            var width = panelRect.width * scaleX;
            var height = panelRect.height * scaleY;
            var bottom = screenSize.y - panelRect.yMax * scaleY;
            return new Rect(panelRect.xMin * scaleX, bottom, width, height);
        }
    }
}
