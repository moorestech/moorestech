using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.Construction;
using Game.PlacementTarget;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// 必要素材に所持数と不足フラグを付与
    /// Attaches held count and shortage flag to required items
    /// </summary>
    public static class BuildMenuMaterialAvailability
    {
        public static List<BuildMenuRequiredItemDto> CreateRequiredItemDtos(IPlacementTarget target, bool freeBlockPlacement, ConstructionWalletQuery walletQuery, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            // 支払いスキップ時は不足を立てない
            // Skip raising shortage when payment is skipped
            var paymentSkipped = freeBlockPlacement || IsCoveredByWallet(target, walletQuery);

            var itemDtos = new List<BuildMenuRequiredItemDto>();
            foreach (var (itemGuid, count) in target.CreateRequiredItems())
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                heldByItem.TryGetValue(itemId, out var held);
                itemDtos.Add(new BuildMenuRequiredItemDto
                {
                    ItemId = itemId.AsPrimitive(),
                    Count = count,
                    Held = held,
                    Lacking = !paymentSkipped && held < count,
                });
            }
            return itemDtos;
        }

        // 財布無しの種別は常に支払う
        // Kinds without a wallet always pay
        private static bool IsCoveredByWallet(IPlacementTarget target, ConstructionWalletQuery walletQuery)
        {
            if (target.Kind != PlacementTargetKind.Block) return false;
            return walletQuery.IsCoveredByWallet(((BlockPlacementTarget)target).BlockId);
        }
    }
}
