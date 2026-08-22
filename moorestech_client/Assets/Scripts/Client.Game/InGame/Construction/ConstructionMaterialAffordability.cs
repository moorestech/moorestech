using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    /// 所持素材だけで建設コスト何セット分を賄えるかを数える。財布は関与しない
    /// Counts how many construction-cost sets the held materials cover; the wallet plays no part here
    /// </summary>
    public static class ConstructionMaterialAffordability
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
    }
}
