using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 設置予定セル分の不足素材をツールチップへ積む
    /// Pushes the materials short for the cells about to be placed onto the tooltip
    /// </summary>
    public static class ConstructionMaterialShortageReporter
    {
        // Placeableを落とす前に呼ぶ（落とした後では不足が消えて理由を出せない）
        // Call before Placeable is cleared (afterwards the shortage disappears and no reason can be shown)
        public static void ReportShortages(List<PlaceInfo> currentPlaceInfos, BlockId representativeBlockId, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            // デバッグ中はコスト判定をスキップ
            // Skip cost checks during debug placement
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            var placeableCellCount = 0;
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (currentPlaceInfos[i].Placeable) placeableCellCount++;
            }

            // 財布の残りで賄えるセルは支払わないため、必要コストセット数を財布へ問い合わせる
            // Cells covered by the wallet remainder are not paid for, so the required cost sets come from the wallet
            var requiredCostSets = walletQuery.GetRequiredCostSets(representativeBlockId, placeableCellCount);
            if (requiredCostSets == 0) return;

            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(representativeBlockId).RequiredItems;
            foreach (var shortage in ConstructionCostShortageCalculator.Calculate(requiredItems, requiredCostSets, inventoryItems)) feedback.Add(ConstructionMaterialShortageLine.ToLine(shortage));
        }
    }
}
