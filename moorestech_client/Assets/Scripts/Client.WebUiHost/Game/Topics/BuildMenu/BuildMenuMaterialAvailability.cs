using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Common.Debug;
using Core.Master;
using Game.Construction;
using Game.PlacementTarget;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// ビルドメニュー1エントリの必要素材へ所持数と不足フラグを付ける
    /// Attaches the held count and shortage flag to one build-menu entry's required items
    /// </summary>
    public static class BuildMenuMaterialAvailability
    {
        public static List<BuildMenuRequiredItemDto> CreateRequiredItemDtos(IPlacementTarget target, ConstructionWalletQuery walletQuery, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            // 支払いが発生しない局面では所持数だけ見せて不足は立てない
            // Where no payment happens the held count still shows, but no shortage stands
            var paymentSkipped = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement) || IsCoveredByWallet(target, walletQuery);

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

        // 財布の有無も残りも財布へ問い合わせる。財布を持たない種別は常に支払いが起きる
        // Both wallet presence and the remainder come from the wallet; kinds without one always pay
        private static bool IsCoveredByWallet(IPlacementTarget target, ConstructionWalletQuery walletQuery)
        {
            if (target.Kind != PlacementTargetKind.Block) return false;
            return walletQuery.IsCoveredByWallet(((BlockPlacementTarget)target).BlockId);
        }
    }
}
