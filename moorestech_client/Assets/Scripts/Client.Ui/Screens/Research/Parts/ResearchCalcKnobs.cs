using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// Web の calc() を通る 2 つの値を customStyle から読んで inline で当てる（A-47）
    /// Reads the two values that go through Web calc() from customStyle and applies them inline (A-47)
    /// </summary>
    public static class ResearchCalcKnobs
    {
        private const string InventoryWidthProperty = "--research-tree-inventory-width";
        private const string ColumnGapProperty = "--research-tree-column-gap";
        private const string ScrollBleedProperty = "--research-tree-scroll-bleed";

        // 研究エリアの left = 持ち物の幅 + 列の間隔（Web .researchArea の calc）
        // Research area left = inventory width + column gap (the Web .researchArea calc)
        public static void ApplyAreaLeft(VisualElement area)
        {
            area.RegisterCallback<CustomStyleResolvedEvent>(_ =>
            {
                // 読めないときは UssCustomStyleReader が LogError を出す
                // When unreadable, UssCustomStyleReader logs an error
                if (!UssCustomStyleReader.TryReadPixels(area.customStyle, InventoryWidthProperty, out var width)) return;
                if (!UssCustomStyleReader.TryReadPixels(area.customStyle, ColumnGapProperty, out var gap)) return;
                area.style.left = width + gap;
            });
        }

        // スクロールを左右へ逃げの分はみ出させ、中身を同じだけ内へ戻す（Web .detailScroll の負の margin-inline）
        // Bleed the scroll out sideways by the clearance and step the content back in (the Web .detailScroll negative margin-inline)
        public static void ApplyScrollBleed(VisualElement scroll, VisualElement content)
        {
            scroll.RegisterCallback<CustomStyleResolvedEvent>(_ =>
            {
                if (!UssCustomStyleReader.TryReadPixels(scroll.customStyle, ScrollBleedProperty, out var bleed)) return;
                scroll.style.marginLeft = -bleed;
                scroll.style.marginRight = -bleed;
                content.style.paddingLeft = bleed;
                content.style.paddingRight = bleed;
            });
        }
    }
}
