using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Anchor;
using Client.Ui.State.PauseMenu;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの子画面の枠。本文だけが縦にスクロールし、戻るボタンは枠内に固定する（Web の PauseMenuSubPage）
    /// Pause sub-page frame: only the body scrolls vertically while the Back button stays fixed in the frame (Web PauseMenuSubPage)
    /// </summary>
    public sealed class PauseMenuSubPageView : VisualElement
    {
        private readonly ScrollView _body;

        public PauseMenuSubPageView(IPauseMenuCommands commands)
        {
            AddToClassList("pause-menu__sub-page");
            _body = new ScrollView(ScrollViewMode.Vertical) { name = "pause-menu-body", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _body.AddToClassList("pause-menu__body");
            Add(_body);

            var back = new PanelActionButton("pause-menu-back", LocalizationKeys.Ui.PauseMenu.Back);
            back.AddToClassList("pause-menu__back");
            TutorialAnchor.Mark(back, AnchorIds.PauseBackToTop);
            back.clicked += () => commands.ShowPage(PauseMenuPage.Top);
            Add(back);
        }

        public void AddSection(VisualElement section)
        {
            PauseMenuStack.Add(_body, section);
        }
    }
}
