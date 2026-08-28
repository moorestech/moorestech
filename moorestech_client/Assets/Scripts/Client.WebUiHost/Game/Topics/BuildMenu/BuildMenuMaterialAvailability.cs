using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// 必要素材に所持数と不足フラグを付与
    /// Attaches held count and shortage flag to required items
    /// </summary>
    public static class BuildMenuMaterialAvailability
    {
        public static List<BuildMenuRequiredItemDto> CreateRequiredItemDtos(IPlacementTarget target, bool paymentSkipped, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            // 同一アイテムの必要数は初出順で合算する（設置時判定と同じ数え方）
            // Required counts of the same item sum in first-seen order, matching how placement decides
            var requiredByItem = new Dictionary<ItemId, int>();
            var itemOrder = new List<ItemId>();
            foreach (var (itemGuid, count) in target.CreateRequiredItems())
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                if (!requiredByItem.ContainsKey(itemId))
                {
                    requiredByItem[itemId] = 0;
                    itemOrder.Add(itemId);
                }
                requiredByItem[itemId] += count;
            }

            var itemDtos = new List<BuildMenuRequiredItemDto>();
            foreach (var itemId in itemOrder)
            {
                heldByItem.TryGetValue(itemId, out var held);
                var required = requiredByItem[itemId];
                itemDtos.Add(new BuildMenuRequiredItemDto
                {
                    ItemId = itemId.AsPrimitive(),
                    Count = required,
                    Held = held,
                    // 支払いスキップ時は不足を立てない
                    // Skip raising shortage when payment is skipped
                    Lacking = !paymentSkipped && held < required,
                });
            }
            return itemDtos;
        }
    }
}
