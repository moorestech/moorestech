using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 建設コストで賄える設置セル数を算出
    /// Calculates how many placement cells the held materials can afford
    /// </summary>
    public static class ConstructionCostPreviewCalculator
    {
        public static int CalculateAffordableCellCount(ConstructionRequiredItemElement[] requiredItems, IEnumerable<IItemStack> inventoryItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return int.MaxValue;

            // 素材ごとの所持数からセル数の最小値を取る
            // Take the minimum affordable cells across materials
            var affordableCellCount = int.MaxValue;
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var total = 0;
                foreach (var stack in inventoryItems)
                {
                    if (stack.Id != itemId) continue;
                    total += stack.Count;
                }
                affordableCellCount = Math.Min(affordableCellCount, total / requiredItem.Count);
            }

            return affordableCellCount;
        }

        /// <summary>
        /// 残り設置数 + 所持素材で買えるセット数×設置数/1セット を返す（ADR 0026）
        /// Returns remaining placements + affordable sets × placementsPerCost (ADR 0026)
        /// </summary>
        public static int CalculateAffordablePlacementCount(ConstructionRequiredItemElement[] requiredItems, int placementsPerCost, int remainingCount, IEnumerable<IItemStack> inventoryItems)
        {
            var affordableSets = CalculateAffordableCellCount(requiredItems, inventoryItems);
            if (affordableSets == int.MaxValue) return int.MaxValue;

            // 大量所持でのオーバーフローを避ける
            // Avoid overflow on very large holdings
            var total = remainingCount + (long)affordableSets * placementsPerCost;
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
