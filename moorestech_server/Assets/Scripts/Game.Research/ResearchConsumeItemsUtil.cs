using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.ResearchModule;

namespace Game.Research
{
    /// <summary>
    ///     研究の消費素材の充足判定と消費。基準の要求には上位の変種も数え、消費は下位のレベルから行う
    ///     Research consume-item sufficiency and consumption; a base requirement also counts higher variants, and consumption starts at the lowest level
    /// </summary>
    internal static class ResearchConsumeItemsUtil
    {
        public static bool HasRequiredItems(ConsumeItemsElement[] consumeItems, IReadOnlyList<IItemStack> inventoryItems)
        {
            foreach (var consumeItem in consumeItems)
            {
                var requiredItemId = MasterHolder.ItemMaster.GetItemId(consumeItem.ItemGuid);
                if (CountAccepted(requiredItemId, inventoryItems) < consumeItem.ItemCount) return false;
            }
            return true;
        }

        public static void Consume(ConsumeItemsElement[] consumeItems, IOpenableInventory inventory)
        {
            foreach (var consumeItem in consumeItems)
            {
                // 下位のレベルから順に、同じレベルの中はスロット順に減らす
                // Take from the lowest level upward, and in slot order within one level
                var remaining = consumeItem.ItemCount;
                var requiredItemId = MasterHolder.ItemMaster.GetItemId(consumeItem.ItemGuid);
                foreach (var acceptedItemId in LevelFamilySubstituteUtil.GetAcceptedItemIds(requiredItemId))
                {
                    for (var slot = 0; slot < inventory.GetSlotSize() && 0 < remaining; slot++)
                    {
                        var stack = inventory.GetItem(slot);
                        if (stack.Id != acceptedItemId) continue;

                        var amount = Math.Min(stack.Count, remaining);
                        inventory.SetItem(slot, stack.SubItem(amount));
                        remaining -= amount;
                    }
                }
            }
        }

        private static int CountAccepted(ItemId requiredItemId, IReadOnlyList<IItemStack> inventoryItems)
        {
            var total = 0;
            foreach (var stack in inventoryItems)
            {
                if (LevelFamilySubstituteUtil.ResolveAcceptedLevel(requiredItemId, stack.Id).HasValue) total += stack.Count;
            }
            return total;
        }
    }
}
