using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Core.Master;
using Game.Construction;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;

namespace Client.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// 必要素材の行（Web の橋 BuildMenuMaterialAvailability と CreateBlueprintRequiredItems の写し）
    /// Required-material rows (port of the Web bridge BuildMenuMaterialAvailability and CreateBlueprintRequiredItems)
    /// </summary>
    public static class BuildMenuMaterialRowsBuilder
    {
        public static IReadOnlyList<BuildMenuMaterialRow> ForTarget(IPlacementTarget target, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            return ToRows(ConstructionCostShortageCalculator.CalculateRequirements(target.CreateRequiredItems(), heldByItem));
        }

        // BP は貼り付けの費用。無料設置で払う物が 0 なら免除し、表示は払うときの費用を出す
        // Blueprints show the paste cost; with free placement and nothing payable it is waived, still listing the full cost
        public static (IReadOnlyList<BuildMenuMaterialRow> rows, bool paymentWaived) ForBlueprint(BlueprintPlacementTarget blueprint, ConstructionWalletQuery walletQuery, bool freeBlockPlacement, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            var drafts = new[] { BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint.Blueprint) };
            var payable = BlueprintPasteCostCalculator.CalcRequiredItems(drafts, walletQuery, freeBlockPlacement);
            var paymentWaived = freeBlockPlacement && payable.Count == 0;
            var required = paymentWaived ? BlueprintPasteCostCalculator.CalcRequiredItems(drafts, walletQuery, false) : payable;
            return (ToRows(ConstructionMaterialAccounting.MatchRequirements(required, heldByItem)), paymentWaived);
        }

        private static IReadOnlyList<BuildMenuMaterialRow> ToRows(IReadOnlyList<(ItemId itemId, int held, int required)> requirements)
        {
            var rows = new List<BuildMenuMaterialRow>();
            foreach (var (itemId, held, required) in requirements)
            {
                var item = new BuildMenuRequiredItem(MasterHolder.ItemMaster.GetItemGuid(itemId), required, held, held < required);
                rows.Add(new BuildMenuMaterialRow(itemId, item));
            }
            return rows;
        }
    }
}
