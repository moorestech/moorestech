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
        /// <summary>
        /// エンティティ列の先頭から所持素材で賄える個数を返す
        /// Returns how many leading entities the inventory can afford
        /// </summary>
        public static int CalculateAffordableEntityCount(IReadOnlyList<ConstructionRequiredItemElement[]> entityCosts, IEnumerable<IItemStack> inventoryItems)
        {
            var remaining = new Dictionary<ItemId, int>();
            foreach (var stack in inventoryItems)
            {
                remaining.TryGetValue(stack.Id, out var current);
                remaining[stack.Id] = current + stack.Count;
            }

            var affordableCount = 0;
            foreach (var cost in entityCosts)
            {
                if (cost == null || cost.Length == 0) { affordableCount++; continue; }

                // 全素材が足りる場合のみ消費を確定して次へ
                // Advance only when every material of this entity is affordable, then commit consumption
                var canAfford = true;
                foreach (var requiredItem in cost)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                    remaining.TryGetValue(itemId, out var held);
                    if (held < requiredItem.Count) { canAfford = false; break; }
                }
                if (!canAfford) break;

                foreach (var requiredItem in cost)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                    remaining[itemId] -= requiredItem.Count;
                }
                affordableCount++;
            }
            return affordableCount;
        }
    }
}
