using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// 必要素材に所持数と不足フラグを付与
    /// Attaches held count and shortage flag to required items
    /// </summary>
    public static class BuildMenuMaterialAvailability
    {
        public static List<BuildMenuRequiredItemDto> CreateRequiredItemDtos(IPlacementTarget target, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            // 合算と突き合わせは設置時判定と同じ唯一の定義へ委ねる
            // Aggregation and matching go through the same single definition placement uses
            var requirements = ConstructionCostShortageCalculator.CalculateRequirements(target.CreateRequiredItems(), heldByItem);

            var itemDtos = new List<BuildMenuRequiredItemDto>();
            foreach (var (itemId, held, required) in requirements)
            {
                itemDtos.Add(new BuildMenuRequiredItemDto
                {
                    ItemId = itemId.AsPrimitive(),
                    Count = required,
                    Held = held,
                    // 素材の事実だけを立てる。支払い免除はエントリ側のPaymentWaivedが持つ
                    // Carries the material fact alone; the payment waiver lives in the entry's PaymentWaived
                    Lacking = held < required,
                });
            }
            return itemDtos;
        }
    }
}
