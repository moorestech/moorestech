using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// コストを素材ごとに合算
    /// Sums construction costs per material
    /// 不足素材のみ返す
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
            var requiredItems = new List<(Guid itemGuid, int count)>();
            foreach (var cost in entityCosts)
            {
                if (cost == null) continue;
                foreach (var requiredItem in cost) requiredItems.Add((requiredItem.ItemGuid, requiredItem.Count));
            }

            // 所持集計は唯一の供給点へ委ねる
            // Delegate the held tally to its single supply point
            var requirements = CalculateRequirements(requiredItems, ConstructionMaterialHeldCounts.Tally(inventoryItems));

            // 所持が必要に満たない素材だけを返す
            // Return only materials whose held count is below the required count
            var shortages = new List<ConstructionMaterialShortage>();
            foreach (var (itemId, held, required) in requirements)
            {
                if (held < required) shortages.Add(new ConstructionMaterialShortage(itemId, held, required));
            }
            return shortages;
        }

        // 必要数と所持数の突き合わせの唯一の定義。不足として扱うかは呼び出し元が決める
        // The single definition of matching required against held; whether that counts as a shortage is the caller's call
        public static List<(ItemId itemId, int held, int required)> CalculateRequirements(IReadOnlyList<(Guid itemGuid, int count)> requiredItems, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            // 必要数を素材の初出順で合算する（表示順を安定させる）
            // Sum required counts per material in first-seen order (keeps the display order stable)
            var requiredByItem = new Dictionary<ItemId, int>();
            var itemOrder = new List<ItemId>();
            foreach (var (itemGuid, count) in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                if (!requiredByItem.ContainsKey(itemId))
                {
                    requiredByItem[itemId] = 0;
                    itemOrder.Add(itemId);
                }
                requiredByItem[itemId] += count;
            }

            var requirements = new List<(ItemId itemId, int held, int required)>();
            foreach (var itemId in itemOrder)
            {
                heldByItem.TryGetValue(itemId, out var held);
                requirements.Add((itemId, held, requiredByItem[itemId]));
            }
            return requirements;
        }
    }
}
