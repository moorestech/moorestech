using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の View が共通に使う小さな手続き（文字の行・表示の出し入れ・表示中の判定）
    /// Small helpers shared by the special views: text rows, display toggling and the displayed check
    /// </summary>
    public static class SpecialViewParts
    {
        public static LocalizedLabel Label(string testid, params string[] classes)
        {
            var label = new LocalizedLabel { name = testid };
            foreach (var className in classes) label.AddToClassList(className);
            SeedEmptyText(label);
            return label;
        }

        // 区画は詳細の前から載るので、載せたときの「文なし」の報告を避けるため空の原文で初期化する（前例 FluidInventoryRow）
        // Sections mount before their detail arrives, so start from an empty verbatim text to avoid the attached-without-text report (precedent FluidInventoryRow)
        public static void SeedEmptyText(LocalizedLabel label)
        {
            label.SetText(UiText.FromVerbatim(string.Empty));
        }

        // 子の出し入れ。UiGapContainer の項目（SetItemShown を使う）と SectionStack の項目としての区画の根には使わない。本文の唯一の子として包む区画にはよい
        // Toggles a child; not for UiGapContainer items (use SetItemShown) or section roots as SectionStack items; fine for a section wrapped as a body's only child
        public static void SetShown(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // パネルに載り、自分から根まで display:none が無い（Web の「層が blockInventory」の代わり）
        // Attached to a panel with no display:none from itself to the root (stands in for the Web "active layer is blockInventory")
        public static bool IsDisplayedInHierarchy(VisualElement element)
        {
            if (element.panel == null) return false;
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return true;
        }
    }
}
