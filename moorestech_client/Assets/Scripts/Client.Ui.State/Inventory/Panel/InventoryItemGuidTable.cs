using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 「このアイテムを持っているスロット」の表（Web InventoryPanel の firstSlots。id は View が inventoryItemAnchorId と同じ形で組む）
    /// Table of "the slot holding this item" (Web InventoryPanel firstSlots; the view builds the id like inventoryItemAnchorId)
    /// </summary>
    public static class InventoryItemGuidTable
    {
        public static IReadOnlyDictionary<int, string> Build(IReadOnlyList<SlotItem> mainSlots)
        {
            // 空と個数 0 を除いた先頭スロットにだけ付ける。C# のマスタは全アイテムが GUID を持つ
            // Only the first owned slot gets it (empty and zero-count excluded); every C# master item has a GUID
            var result = new Dictionary<int, string>();
            foreach (var pair in OwnedItemCounts.FirstSlotIndexByItemId(mainSlots))
            {
                var guid = MasterHolder.ItemMaster.GetItemGuid(pair.Key);
                result[pair.Value] = guid.ToString();
            }
            return result;
        }
    }
}
