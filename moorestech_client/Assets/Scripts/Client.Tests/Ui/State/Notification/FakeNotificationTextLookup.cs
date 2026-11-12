using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using Client.Ui.State.Notification.Text;
using Core.Master;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 内容名は "resolved:<キー>"、アイテム名は登録した名前か "item:<id>" を返し、呼ばれた回数を数える（Web テストの translate と resolveItemDisplayName）
    /// Returns "resolved:<key>" for content names and a registered name or "item:<id>" for items, counting calls (Web test translate and resolveItemDisplayName)
    /// </summary>
    public sealed class FakeNotificationTextLookup : INotificationTextLookup
    {
        private readonly Dictionary<ItemId, string> _itemNames = new();

        public int ContentTextCalls { get; private set; }
        public int ItemNameCalls { get; private set; }

        public void SetItemName(ItemId itemId, string name)
        {
            _itemNames[itemId] = name;
        }

        public string ContentText(ContentLocalizationKey key)
        {
            ContentTextCalls++;
            return $"resolved:{key.Key}";
        }

        public string ItemDisplayName(ItemId itemId)
        {
            ItemNameCalls++;
            return _itemNames.TryGetValue(itemId, out var name) ? name : $"item:{itemId.AsPrimitive()}";
        }
    }
}
