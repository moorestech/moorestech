using Client.Ui.Content;
using Client.Ui.Foundation.Icons;
using Client.Ui.State.Notification.Text;
using Core.Master;
using UnityEngine;

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
            // マスタに無い ID は代わりのアイコンにする（throw すると購読が止まり後続の通知が出ない）。実績・拒否の本文はアイテム名を引かないので、理由はここで必ず出す。獲得では本文の口と合わせて 2 本出る
            // An id absent from the master gets the fallback icon (throwing would stop the subscription and every later notification); achievement/denied text never looks up the item name, so log the reason here; an earn logs twice together with the text lookup
            if (_content.TryItem(itemId, out var icon)) return icon;
            var idText = NotificationText.ItemIdText(itemId);
            Debug.LogError($"[GameContentNotificationRowIcons] item {idText} is not in the item master; showing the fallback icon");
            return new UiIcon(null, idText, NotificationText.FallbackItemName(itemId));
        }
    }
}
