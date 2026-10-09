using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
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
            var requirements = CalculateRequirements(requiredItems, ConstructionMaterialAccounting.TallyHeld(inventoryItems));

            return ToShortages(requirements);
        }

        // 突き合わせ結果から不足だけを抜き出す唯一の定義
        // The single definition extracting the shortages out of a requirement match
        public static List<ConstructionMaterialShortage> ToShortages(IReadOnlyList<(ItemId itemId, int held, int required)> requirements)
        {
            var shortages = new List<ConstructionMaterialShortage>();
            foreach (var (itemId, held, required) in requirements)
            {
                if (held < required) shortages.Add(new ConstructionMaterialShortage(itemId, held, required));
            }
            return shortages;
        }

        // guidをitemIdへ解決してから突き合わせる
        // Resolves guids to item ids before the same match
        public static List<(ItemId itemId, int held, int required)> CalculateRequirements(IReadOnlyList<(Guid itemGuid, int count)> requiredItems, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            var resolved = new List<(ItemId itemId, int count)>(requiredItems.Count);
            foreach (var (itemGuid, count) in requiredItems) resolved.Add((MasterHolder.ItemMaster.GetItemId(itemGuid), count));
            return ConstructionMaterialAccounting.MatchRequirements(resolved, heldByItem);
        }
    }
}
