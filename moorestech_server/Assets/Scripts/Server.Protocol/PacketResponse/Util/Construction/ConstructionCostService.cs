using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 建設コスト(requiredItems)の検証・消費・返却スタック生成を行う
    /// Validates, consumes, and creates refund stacks for construction costs (requiredItems)
    /// </summary>
    public static class ConstructionCostService
    {
        public static bool HasRequiredItems(ConstructionRequiredItemElement[] requiredItems, IReadOnlyList<IItemStack> inventoryItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return true;

            // 全スロットの所持数を合算
            // Sum held counts across all inventory slots per material
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var total = 0;
                foreach (var stack in inventoryItems)
                {
                    if (stack.Id != itemId) continue;
                    total += stack.Count;
                }
                if (total < requiredItem.Count) return false;
            }

            return true;
        }

        public static void ConsumeRequiredItems(ConstructionRequiredItemElement[] requiredItems, IOpenableInventory inventory)
        {
            if (requiredItems == null || requiredItems.Length == 0) return;

            // 先頭スロットから順に減算する共通処理（電線消費と同一実装）を再利用する
            // Reuse the shared first-slot-onward consumption logic used by wire consumption
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                ElectricWireSystemUtil.ConsumeItem(inventory, itemId, requiredItem.Count);
            }
        }

        public static List<IItemStack> CreateRefundItems(ConstructionRequiredItemElement[] requiredItems)
        {
            var result = new List<IItemStack>();
            if (requiredItems == null) return result;

            // コスト全額分のスタック生成
            // Create refund stacks matching the full cost definition
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                result.Add(ServerContext.ItemStackFactory.Create(itemId, requiredItem.Count));
            }

            return result;
        }
    }
}
