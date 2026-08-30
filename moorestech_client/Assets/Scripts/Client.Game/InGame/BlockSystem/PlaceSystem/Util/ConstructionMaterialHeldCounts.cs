using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 所持スタック列からitemId別の所持数を集計する
    /// Tallies held counts per itemId from a sequence of item stacks
    /// </summary>
    public static class ConstructionMaterialHeldCounts
    {
        public static Dictionary<ItemId, int> Tally(IEnumerable<IItemStack> inventoryItems)
        {
            var heldByItem = new Dictionary<ItemId, int>();
            foreach (var stack in inventoryItems)
            {
                heldByItem.TryGetValue(stack.Id, out var current);
                heldByItem[stack.Id] = current + stack.Count;
            }
            return heldByItem;
        }
    }
}
