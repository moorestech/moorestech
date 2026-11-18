using Client.Ui.Foundation.Icons;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Text;
using Core.Master;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// テクスチャを持たず代わりの文字 "#<id>" だけのアイコンを返す（Web の e2e で ItemIcon が "#2" を出すのと同じ見え方）
    /// Returns texture-less icons with only the "#<id>" fallback text (same look as the Web e2e ItemIcon showing "#2")
    /// </summary>
    public sealed class FakeNotificationRowIcons : INotificationRowIcons
    {
        public UiIcon ItemIcon(ItemId itemId)
        {
            var idText = $"#{itemId.AsPrimitive()}";
            return new UiIcon(null, idText, UiText.FromVerbatim(idText));
        }
    }
}
