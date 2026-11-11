using Client.Ui.Foundation.Icons;
using Core.Master;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の行の頭に出すアイテムのアイコンを引く口（テストでは代わりの文字だけのアイコンを返す）
    /// Looks up the item icon at the head of a notification row (tests return fallback-text-only icons)
    /// </summary>
    public interface INotificationRowIcons
    {
        UiIcon ItemIcon(ItemId itemId);
    }
}
