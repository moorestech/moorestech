using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// パネルの骨（帯・額縁・閉じる・3 列・中央列の検索とスクロール）。Web BuildMenuPanel の JSX の並びを写す
    /// Panel skeleton (band, frame, close, three columns, the center column's search and scroll), copying the Web BuildMenuPanel JSX order
    /// </summary>
    public sealed class BuildMenuPanelSkeleton
    {
        public VisualElement Band { get; }
        public IconButton CloseButton { get; }
        public GameTextField Search { get; }
        public GameScrollView Scroll { get; }

        private readonly LocalizedLabel _noResults = new() { name = "build-menu-no-results" };
        private readonly BuildMenuCategoryList _list;

        public BuildMenuPanelSkeleton(BuildMenuCategorySidebar sidebar, BuildMenuCategoryList list, BuildMenuDetailSidebar detail)
        {
            _list = list;

            // 帯は舞台の安全帯の間で拾わず、パネルだけが拾う（Web の pointer-events: none / auto）
            // The band between the stage safe areas never picks; only the panel does (the Web pointer-events none / auto)
            Band = new VisualElement { pickingMode = PickingMode.Ignore };
            Band.AddToClassList("build-menu__band");
            var panel = new VisualElement { name = "build-menu-panel" };
            panel.AddToClassList("build-menu__panel");
            Band.Add(panel);

            var frame = new GamePanel("build-menu-frame", GamePanelFrame.Default);
            frame.AddToClassList("build-menu__frame");
            frame.SetTitle(new UiText(LocalizationKeys.Ui.BuildMenu.Title)); /* look-exception: LX-72。題の影のかけらと 2560×1080 の幅は共有 GamePanel の題の描き方 */
            panel.Add(frame);

            // 中央列: 検索→スクロール
            // Center column: search, then the scroll (the no-results label swaps with the list)
            Search = new GameTextField("build-menu-search", false, GameTextFieldLook.GaugeInset);
            Search.AddToClassList("build-menu__search");
            Search.SetPlaceholder(new UiText(LocalizationKeys.Ui.BuildMenu.SearchPlaceholder));
            Scroll = new GameScrollView("build-menu-scroll-area");
            Scroll.AddToClassList("build-menu__scroll");
            _noResults.AddToClassList(UiTypographyClass.TextMd);
            _noResults.AddToClassList("build-menu__no-results");
            _noResults.SetText(new UiText(LocalizationKeys.Ui.BuildMenu.NoResults));
            Scroll.Add(_noResults);
            Scroll.Add(list);
            var main = new VisualElement();
            main.AddToClassList("build-menu__main");
            main.Add(Search);
            main.Add(Scroll);

            // カテゴリ | 検索+スクロール | 詳細 の 3 列。列の間は 2 列目以降の左の余白（UITK に gap が無い）
            // Three columns: categories | search + scroll | detail; gaps are left margins from the second column on (UITK has no gap)
            main.AddToClassList("build-menu__column-following");
            detail.AddToClassList("build-menu__column-following");
            var columns = new VisualElement { name = "build-menu-columns" };
            columns.AddToClassList("build-menu__columns");
            columns.Add(sidebar);
            columns.Add(main);
            columns.Add(detail);
            frame.Add(columns);

            // 閉じるは Web と同じく GamePanel の本文の子（top・right は題の下の本文から測る）。列の後に足して前面へ重ねる（Web の z-index の代わり）
            // The close button is a child of the GamePanel body as on the Web (top/right measured from the body under the title); added after the columns so it draws in front (instead of the Web z-index)
            CloseButton = new IconButton("build-menu-close", new IconButtonCloseGlyph());
            CloseButton.AddToClassList("build-menu__close");
            frame.Add(CloseButton);
            SetNoResults(false);
        }

        // 該当なしはリストと入れ替える（Web の三項。D-28）
        // The no-results label replaces the list (the Web ternary; D-28)
        public void SetNoResults(bool showsNoResults)
        {
            _noResults.style.display = showsNoResults ? DisplayStyle.Flex : DisplayStyle.None;
            _list.style.display = showsNoResults ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
