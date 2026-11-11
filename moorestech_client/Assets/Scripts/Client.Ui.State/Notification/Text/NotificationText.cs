using System;
using System.Collections.Generic;
using System.Globalization;
using Client.Localization;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Notification.Text
{
    /// <summary>
    /// 中身から表示の UiText を作る。キーと補間値を同じ分岐で決め、アイテム名と数を持つのは獲得だけという約束を 1 か所に閉じる（Web resolveNotificationText）
    /// Builds the display UiText from a content; the key and values come from one branch, closing the "only earns carry an item name and count" contract in one place (Web resolveNotificationText)
    /// </summary>
    public static class NotificationText
    {
        public static UiText Resolve(NotificationContent content, INotificationTextLookup lookup)
        {
            var values = BuildInterpolationValues(content.MessageId, ResolveParams(content.MessageParams, lookup));

            // 獲得は種別でキーを決め、messageId のずれで生のトークンが出るのを防ぐ。ほかは表で引く
            // An earn fixes its key by kind so a drifting messageId cannot leak raw tokens; others use the table
            if (content is not EarnedNotificationContent earned) return new UiText(NotificationMessageKeys.Resolve(content.MessageId), values);

            // Web は { ...values, itemName, count } で実値が後勝ち、C# の補間は先勝ちで重複を LogError にするので、同名の引数を除いてから実値を足す
            // Web lets the real values win via { ...values, itemName, count }; C# interpolation keeps the first and logs duplicates, so drop same-named params before adding the real values
            values.RemoveAll(param => param.Name == "itemName" || param.Name == "count");
            values.Add(new LocalizationTextParam("itemName", lookup.ItemDisplayName(earned.ItemId)));
            values.Add(new LocalizationTextParam("count", earned.Count.ToString(CultureInfo.InvariantCulture)));
            return new UiText(LocalizationKeys.Ui.Notification.ItemEarned, values);
        }

        // マスタに無い・名前が欠けたアイテムの代わりの名前を作る唯一の場所（獲得の本文もアイコンの hover もここを使う）
        // The only place that builds the fallback name for an item absent from the master or missing its name (earn text and icon hover both use it)
        public static UiText FallbackItemName(ItemId itemId)
        {
            return new UiText(LocalizationKeys.Ui.Common.ItemFallback, new[] { new LocalizationTextParam("itemId", ItemIdText(itemId)) });
        }

        // ItemId の文字列化はここだけ（InvariantCulture）
        // The single ItemId stringification (InvariantCulture)
        public static string ItemIdText(ItemId itemId)
        {
            return itemId.AsPrimitive().ToString(CultureInfo.InvariantCulture);
        }

        internal static IReadOnlyList<LocalizationTextParam> ResolveParams(IReadOnlyList<NotificationParam> messageParams, INotificationTextLookup lookup)
        {
            var resolved = new List<LocalizationTextParam>(messageParams.Count);
            foreach (var param in messageParams) resolved.Add(new LocalizationTextParam(param.Name, ValueOf(param)));
            return resolved;

            #region Internal

            // 内容名の引数だけ、いまの言語の表示名を引く。サーバーは表示名でなく Guid を送る
            // Only content-name arguments look up the display name in the current language; the server sends Guids, not names
            string ValueOf(NotificationParam param)
            {
                return param switch
                {
                    TextNotificationParam text => text.Value,
                    ContentNameNotificationParam contentName => lookup.ContentText(contentName.Key),
                    _ => throw new ArgumentOutOfRangeException(nameof(param), param.GetType().Name, null),
                };
            }

            #endregion
        }

        // params を並べた後に messageId を足す。params に同名があれば messageId が勝つ（Web の { ...params, messageId }）
        // List the params, then messageId; a same-named param loses to messageId (Web { ...params, messageId })
        internal static List<LocalizationTextParam> BuildInterpolationValues(string messageId, IReadOnlyList<LocalizationTextParam> resolvedParams)
        {
            var values = new List<LocalizationTextParam>(resolvedParams.Count + 1);
            foreach (var param in resolvedParams)
            {
                if (param.Name != "messageId") values.Add(param);
            }
            values.Add(new LocalizationTextParam("messageId", messageId));
            return values;
        }
    }
}
