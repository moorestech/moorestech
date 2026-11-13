using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.PauseMenu;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの子画面の枠。本文だけが縦にスクロールし、戻るボタンは枠内に固定する（Web の PauseMenuSubPage）
    /// Pause sub-page frame: only the body scrolls vertically while the Back button stays fixed in the frame (Web PauseMenuSubPage)
    /// </summary>
    public sealed class PauseMenuSubPageView : VisualElement
    {
        private readonly UiStack _sections;

        public PauseMenuSubPageView(IPauseMenuCommands commands)
        {
            // 本文のスクロールと戻るを Stack gap=md で縦に並べる
            // Stack the scrolling body and Back with Stack gap=md
            AddToClassList("pause-menu__sub-page");
            var column = new UiStack(UiGap.Md);
            column.AddToClassList("pause-menu__sub-page-column");
            Add(column);

            // 節は本文の中で Stack gap=md に並べる
            // Sections stack with gap=md inside the body
            // Web は本文の basis を内容の高さとして解き、溢れると戻るも行の箱まで縮める
            // The Web resolves the body basis as its content height, so on overflow Back also shrinks to its line box
            var body = new GameScrollView("pause-menu-body", GameScrollAxis.Vertical);
            body.FollowContentHeightAsFlexBasis();
            _sections = new UiStack(UiGap.Md);
            body.Add(_sections);
            column.AddItem(body);

            var back = new PanelActionButton("pause-menu-back", LocalizationKeys.Ui.PauseMenu.Back);
            back.AddToClassList("pause-menu__back");
            TutorialAnchor.Mark(back, AnchorIds.PauseBackToTop);
            back.OnClicked.Subscribe(_ => commands.ShowPage(PauseMenuPage.Top));
            column.AddItem(back);
        }

        public void AddSection(VisualElement section)
        {
            _sections.AddItem(section);
        }
    }
}
