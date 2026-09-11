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
        // 列は張替えのように複数BlockIdが混ざるので、不足はセル自身のBlockIdごとに数える
        // A run can mix BlockIds (e.g. a replace run), so the shortage is counted per the cell's own BlockId
        public static void ReportShortages(List<PlaceInfo> currentPlaceInfos, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            // デバッグ中はコスト判定をスキップ
            // Skip cost checks during debug placement
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            var placeableCellCounts = new Dictionary<BlockId, int>();
            foreach (var placeInfo in currentPlaceInfos)
            {
                if (!placeInfo.Placeable) continue;
                placeableCellCounts.TryGetValue(placeInfo.BlockId, out var count);
                placeableCellCounts[placeInfo.BlockId] = count + 1;
            }

            foreach (var (blockId, placeableCellCount) in placeableCellCounts)
            {
                // 財布の残りで賄えるセルは支払わないため、必要コストセット数を財布へ問い合わせる
                // Cells covered by the wallet remainder are not paid for, so the required cost sets come from the wallet
                var requiredCostSets = walletQuery.GetRequiredCostSets(blockId, placeableCellCount);
                if (requiredCostSets == 0) continue;

                var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems;
                feedback.AddMaterialShortages(ConstructionCostShortageCalculator.Calculate(requiredItems, requiredCostSets, inventoryItems));
            }
        }
    }
}
