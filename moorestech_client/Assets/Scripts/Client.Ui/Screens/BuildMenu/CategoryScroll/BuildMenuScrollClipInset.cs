using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// クリップ境界をチュートリアル枠の逃げぶんセルから離す（Web の .scroll の負の margin・viewport の padding を C# で導く。縦バーの inset は BuildMenu.uss が部品のつまみで上書きする。前例 RecipeEntryList.ApplyClipClearance・D-5・D-51）
    /// Keeps the clip edge away from cells by the tutorial ring's clearance (derives the Web .scroll negative margin and viewport padding in C#; BuildMenu.uss overrides the bar inset knobs; precedent RecipeEntryList.ApplyClipClearance, D-5, D-51)
    /// </summary>
    public static class BuildMenuScrollClipInset
    {
        // 画面 USS が .build-menu__scroll で --tutorial-anchor-clip-inset を写して宣言する（customStyle は祖先の宣言を読まない。D-26）
        // The screen USS redeclares --tutorial-anchor-clip-inset on .build-menu__scroll (customStyle does not read ancestors; D-26)
        private const string ClipInsetProperty = "--build-menu-scroll-clip-inset";

        public static void Apply(GameScrollView scroll)
        {
            scroll.RegisterCallback<CustomStyleResolvedEvent>(_ => OnResolved());

            #region Internal

            void OnResolved()
            {
                // 読めないときは UssCustomStyleReader が LogError を出し、クリップは広げない（BuildMenuLayoutTest の逃げの主張が赤になる）
                // When unreadable, UssCustomStyleReader logs an error and the clip stays unwidened (BuildMenuLayoutTest's clearance assertion turns red)
                if (!UssCustomStyleReader.TryReadPixels(scroll.customStyle, ClipInsetProperty, out var inset)) return;

                // 外へ広げた分だけ内側を空け、格子の位置と寸法を変えない（Web: margin calc(-1 * inset)・padding inset）
                // Pad inward by what was widened outward so the grid keeps its position and size (Web: margin calc(-1 * inset), padding inset)
                var outer = scroll.style;
                outer.marginLeft = -inset;
                outer.marginTop = -inset;
                outer.marginRight = -inset;
                outer.marginBottom = -inset;
                var content = scroll.contentContainer.style;
                content.paddingLeft = inset;
                content.paddingTop = inset;
                content.paddingRight = inset;
                content.paddingBottom = inset;
            }

            #endregion
        }
    }
}
