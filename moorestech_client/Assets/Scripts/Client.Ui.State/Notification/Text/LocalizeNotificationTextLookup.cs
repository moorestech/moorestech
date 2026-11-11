using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Notification.Text
{
    /// <summary>
    /// 本番の口。コンテンツ辞書から引き、アイテム名はマスタの Guid からキーを作る。欠けは理由つきで LogError を出し、Web と同じ見え方で出す
    /// Production lookup; reads the content dictionary and builds item-name keys from the master Guid; gaps log an error with the reason and show the same as Web
    /// </summary>
    public sealed class LocalizeNotificationTextLookup : INotificationTextLookup
    {
        public string ContentText(ContentLocalizationKey key)
        {
            // 全言語に無い内容名は Localize が欠けの印を返す。Web も同じ印を出すのでそのまま出し、理由を開発者に残す
            // Localize returns the missing marker for a content name absent in every language; Web shows the same marker, so keep it and leave the reason for developers
            var text = Localize.GetContent(key);
            if (LocalizationMissingText.IsMissingText(text, key.Key)) Debug.LogError($"[LocalizeNotificationTextLookup] content text missing in every language: {key.Key}");
            return text;
        }

        public string ItemDisplayName(ItemId itemId)
        {
            // マスタに無いアイテムは Guid を引けない。Web の useItemDisplayName と同じく「Item {itemId}」で出す
            // An item absent from the master has no Guid; show "Item {itemId}" like the Web useItemDisplayName
            if (!MasterHolder.ItemMaster.ExistItemId(itemId))
            {
                Debug.LogError($"[LocalizeNotificationTextLookup] item {itemId.AsPrimitive()} is not in the item master; showing the fallback name");
                return FallbackName(itemId);
            }

            var key = ContentLocalizationKeys.ItemName(MasterHolder.ItemMaster.GetItemGuid(itemId));
            var name = Localize.GetContent(key);
            if (!LocalizationMissingText.IsMissingText(name, key.Key)) return name;
            Debug.LogError($"[LocalizeNotificationTextLookup] item {itemId.AsPrimitive()} has no name in every language ({key.Key}); showing the fallback name");
            return FallbackName(itemId);

            #region Internal

            string FallbackName(ItemId id)
            {
                var itemIdText = id.AsPrimitive().ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Localize.GetFormatted(LocalizationKeys.Ui.Common.ItemFallback, new[] { new LocalizationTextParam("itemId", itemIdText) });
            }

            #endregion
        }
    }
}
