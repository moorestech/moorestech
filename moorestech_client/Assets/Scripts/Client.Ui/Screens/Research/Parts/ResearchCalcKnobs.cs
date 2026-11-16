using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// calc相当の2値をcustomStyleから当てる
    /// Apply two calc values from customStyle
    /// </summary>
    public static class ResearchCalcKnobs
    {
        private const string InventoryWidthProperty = "--research-tree-inventory-width";
        private const string ColumnGapProperty = "--research-tree-column-gap";
        private const string ScrollBleedProperty = "--research-tree-scroll-bleed";

        // left=持ち物幅+列間隔
        // left = inventory width + column gap
        public static void ApplyAreaLeft(VisualElement area)
        {
            area.RegisterCallback<CustomStyleResolvedEvent>(_ =>
            {
                // 読めない時はLogError
                // Unreadable logs an error
                if (!UssCustomStyleReader.TryReadPixels(area.customStyle, InventoryWidthProperty, out var width)) return;
                if (!UssCustomStyleReader.TryReadPixels(area.customStyle, ColumnGapProperty, out var gap)) return;
                area.style.left = width + gap;
            });
        }

        // 左右の逃げ分はみ出し、中身を内へ戻す
        // Bleed sideways and step content back in
        public static void ApplyScrollBleed(VisualElement scroll, VisualElement content)
        {
            scroll.RegisterCallback<CustomStyleResolvedEvent>(_ =>
            {
                if (!UssCustomStyleReader.TryReadPixels(scroll.customStyle, ScrollBleedProperty, out var bleed)) return;
                UiScrollClipInset.ApplyHorizontal(scroll, content, bleed);
            });
        }
    }
}
