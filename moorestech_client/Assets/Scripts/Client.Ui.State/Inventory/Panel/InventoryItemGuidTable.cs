using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// アイテムを持つスロットの表（Web firstSlots）
    /// Table of slots holding each item (Web firstSlots)
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
