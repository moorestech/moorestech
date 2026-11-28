using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;

namespace Client.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の View が共通に使う小さな手続き（文字の行）
    /// Small helpers shared by the special views: text rows
    /// </summary>
    public static class SpecialViewParts
    {
        internal static void RenderUnavailable(LocalizedLabel label, SpecialBlockDetailViewModel detail)
        {
            // 対象の機能が無い場合にだけ専用本文の理由を表示する
            // Show the dedicated body's reason only when the required feature is absent
            var unavailable = detail.Unavailable;
            if (unavailable != null) label.SetText(unavailable);
            UiDisplay.SetShown(label, unavailable != null);
        }

        public static LocalizedLabel Label(string testid, params string[] classes)
        {
            var label = new LocalizedLabel(UiText.Empty) { name = testid };
            foreach (var className in classes) label.AddToClassList(className);
            return label;
        }
    }
}
