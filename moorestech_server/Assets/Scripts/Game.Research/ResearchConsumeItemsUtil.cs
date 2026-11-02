using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.ResearchModule;

namespace Game.Research
{
    internal static class ResearchConsumeItemsUtil
    {
        public static bool HasRequiredItems(ConsumeItemsElement[] consumeItems, IReadOnlyList<IItemStack> inventoryItems)
        {
            return TryAllocate(consumeItems, inventoryItems, out _);
        }

        public static bool TryConsume(ConsumeItemsElement[] consumeItems, IOpenableInventory inventory, out string denyReason)
        {
            // 全要求を満たすまで実在庫を変更しない
            // Keep actual inventory unchanged until every requirement is allocated
            if (!TryAllocate(consumeItems, inventory.InventoryItems, out var amounts))
            {
                denyReason = "研究素材が不足しています";
                UnityEngine.Debug.LogWarning(denyReason);
                return false;
            }
            for (var slot = 0; slot < amounts.Length; slot++)
            {
                if (0 < amounts[slot]) inventory.SetItem(slot, inventory.GetItem(slot).SubItem(amounts[slot]));
            }
            denyReason = null;
            return true;
        }

        private static bool TryAllocate(ConsumeItemsElement[] requirements, IReadOnlyList<IItemStack> items, out int[] amounts)
        {
            amounts = new int[items.Count];
            // 特定変種を先に確保し、基準要求へ残りを配る
            // Reserve exact variants first, then allocate leftovers to base requirements
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var requirement in requirements)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requirement.ItemGuid);
                    var accepted = LevelFamilySubstituteUtil.GetAcceptedItemIds(itemId);
                    if ((accepted.Count == 1) != (pass == 0)) continue;
                    var remaining = requirement.ItemCount;
                    foreach (var acceptedId in accepted)
                    {
                        for (var slot = 0; slot < items.Count && 0 < remaining; slot++)
                        {
                            if (items[slot].Id != acceptedId) continue;
                            var count = Math.Min(items[slot].Count - amounts[slot], remaining);
                            amounts[slot] += count;
                            remaining -= count;
                        }
                    }
                    // silent-ok: 判定は表示にも使う / 呼び出し元が不足状態または拒否理由を提示する
                    if (0 < remaining) return false;
                }
            }
            return true;
        }
    }
}
