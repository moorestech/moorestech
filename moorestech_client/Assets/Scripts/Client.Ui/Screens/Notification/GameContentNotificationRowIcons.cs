using System.Globalization;
using Client.Localization;
using Client.Ui.Content;
using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
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
            // マスタに無い ID は名前もテクスチャも引けないので、理由つきで「Item {id}」と "#id" の代わりのアイコンにする（throw すると購読が止まり後続の通知が出ない）
            // An id absent from the master has no name or texture, so log the reason and show the "Item {id}" / "#id" fallback icon (throwing would stop the subscription and every later notification)
            if (MasterHolder.ItemMaster.ExistItemId(itemId)) return _content.Item(itemId);
            var idText = itemId.AsPrimitive().ToString(CultureInfo.InvariantCulture);
            Debug.LogError($"[GameContentNotificationRowIcons] item {idText} is not in the item master; showing the fallback icon");
            var name = new UiText(LocalizationKeys.Ui.Common.ItemFallback, new[] { new LocalizationTextParam("itemId", idText) });
            return new UiIcon(null, idText, name);
        }
    }
}
