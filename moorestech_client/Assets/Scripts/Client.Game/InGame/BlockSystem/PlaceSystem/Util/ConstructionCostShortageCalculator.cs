using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// コストを素材ごとに合算
    /// 不足素材のみ返す
    /// Sums construction costs per material
    /// Returns only the ones falling short
    /// </summary>
    public static class ConstructionCostShortageCalculator
    {
        public static List<ConstructionMaterialShortage> Calculate(ConstructionRequiredItemElement[] requiredItems, int entityCount, IEnumerable<IItemStack> inventoryItems)
        {
            var entityCosts = new List<ConstructionRequiredItemElement[]>(entityCount);
            for (var i = 0; i < entityCount; i++) entityCosts.Add(requiredItems);
            return Calculate(entityCosts, inventoryItems);
        }

        public static List<ConstructionMaterialShortage> Calculate(IReadOnlyList<ConstructionRequiredItemElement[]> entityCosts, IEnumerable<IItemStack> inventoryItems)
        {
            // 必要数を素材の初出順で合算する（表示順を安定させる）
            // Sum required counts per material in first-seen order (keeps the display order stable)
            var requiredByItem = new Dictionary<ItemId, int>();
            var itemOrder = new List<ItemId>();
            foreach (var cost in entityCosts)
            {
                if (cost == null) continue;
                foreach (var requiredItem in cost)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                    if (!requiredByItem.ContainsKey(itemId))
                    {
                        requiredByItem[itemId] = 0;
                        itemOrder.Add(itemId);
                    }
                    requiredByItem[itemId] += requiredItem.Count;
                }
            }

            // 所持数を集計する
            // Tally held counts
            var heldByItem = new Dictionary<ItemId, int>();
            foreach (var stack in inventoryItems)
            {
                heldByItem.TryGetValue(stack.Id, out var current);
                heldByItem[stack.Id] = current + stack.Count;
            }

            // 所持が必要に満たない素材だけを返す
            // Return only materials whose held count is below the required count
            var shortages = new List<ConstructionMaterialShortage>();
            foreach (var itemId in itemOrder)
            {
                heldByItem.TryGetValue(itemId, out var held);
                var required = requiredByItem[itemId];
                if (held < required) shortages.Add(new ConstructionMaterialShortage(itemId, held, required));
            }
            return shortages;
        }
    }
}
