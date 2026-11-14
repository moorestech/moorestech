using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;

namespace Client.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の View が共通に使う小さな手続き（文字の行）
    /// Small helpers shared by the special views: text rows
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
    }
}
