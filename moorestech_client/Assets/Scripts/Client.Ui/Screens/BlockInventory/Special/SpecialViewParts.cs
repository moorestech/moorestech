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
            var label = new LocalizedLabel(UiText.Empty) { name = testid };
            foreach (var className in classes) label.AddToClassList(className);
            return label;
        }
    }
}
