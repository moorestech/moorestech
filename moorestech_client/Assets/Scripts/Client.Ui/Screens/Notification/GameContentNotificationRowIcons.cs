using System.Globalization;
using Client.Ui.Content;
using Client.Ui.Foundation.Icons;
using Client.Ui.State.Notification.Text;
using Core.Master;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 本番の口。ID からアイコンへの解決は Content 層（GameContentPresentation）の 1 か所に任せる
    /// Production lookup; resolving an id into an icon is left to the single place in the Content layer (GameContentPresentation)
    /// </summary>
    public sealed class GameContentNotificationRowIcons : INotificationRowIcons
    {
        private readonly GameContentPresentation _content;

        public GameContentNotificationRowIcons(GameContentPresentation content)
        {
            _content = content;
        }

        public UiIcon ItemIcon(ItemId itemId)
        {
            // マスタに無い ID は名前もテクスチャも引けないので代わりのアイコンにする（throw すると購読が止まり後続の通知が出ない。理由の LogError は同じ行の本文を引く口が 1 本だけ出す）
            // An id absent from the master has no name or texture, so show the fallback icon (throwing would stop the subscription and every later notification; the text lookup of the same row logs the one error)
            if (MasterHolder.ItemMaster.ExistItemId(itemId)) return _content.Item(itemId);
            var idText = itemId.AsPrimitive().ToString(CultureInfo.InvariantCulture);
            return new UiIcon(null, idText, LocalizeNotificationTextLookup.FallbackItemName(itemId));
        }
    }
}
