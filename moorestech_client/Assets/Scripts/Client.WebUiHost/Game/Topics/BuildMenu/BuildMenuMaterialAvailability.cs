using System.Collections.Generic;
using System.Linq;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.Construction;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// 必要素材に所持数と不足フラグを付与
    /// Attaches held count and shortage flag to required items
    /// </summary>
    public static class BuildMenuMaterialAvailability
    {
        internal static (List<BuildMenuRequiredItemDto> items, bool paymentWaived) CreateBlueprintRequiredItems(
            BlueprintPlacementTarget target, ConstructionWalletQuery wallet, IReadOnlyDictionary<ItemId, int> heldByItem, bool freeBlockPlacement)
        {
            var draft = BlueprintPasteCopyBuilder.BuildUnobstructed(target.Blueprint);

            // 無料設置でも有料チェーンが残るBPは不足表示を維持する
            // Keep shortage display when paid chains remain during free placement
            var hasPaidChains = draft.Lines.Any(line => line.Kind == BlueprintPasteLineKind.GearChain
                && line.Materials.Any(material => 0 < material.Count));
            var paymentWaived = freeBlockPlacement && !hasPaidChains;

            // 完全免除時は通常の必要素材を案内し、それ以外は実際の支払額を一度だけ算出する
            // Show nominal materials for a full waiver; otherwise calculate the payable costs just once
            var required = BlueprintPasteCostCalculator.CalcRequiredItems(new[] { draft }, wallet, freeBlockPlacement && !paymentWaived);
            return (CreateRequiredItemDtos(required, heldByItem), paymentWaived);
        }

        public static List<BuildMenuRequiredItemDto> CreateRequiredItemDtos(IPlacementTarget target, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            // 合算と突き合わせは設置時判定と同じ唯一の定義へ委ねる
            // Aggregation and matching go through the same single definition placement uses
            var requiredItems = new List<(ItemId itemId, int count)>();
            foreach (var (itemGuid, count) in target.CreateRequiredItems())
                requiredItems.Add((MasterHolder.ItemMaster.GetItemId(itemGuid), count));
            return CreateRequiredItemDtos(requiredItems, heldByItem);
        }

        public static List<BuildMenuRequiredItemDto> CreateRequiredItemDtos(IReadOnlyList<(ItemId itemId, int count)> requiredItems, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            var requirements = ConstructionMaterialAccounting.MatchRequirements(requiredItems, heldByItem);

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
