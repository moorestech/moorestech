using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// 後続不足セルをPlaceable=falseに
    /// 不足素材をツールチップへ積む
    /// Marks cells beyond affordability as Placeable=false
    /// Pushes the short materials to the tooltip
    /// </summary>
    public static class CommonBlockPlaceCostMarker
    {
        public static void MarkInsufficientCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockId blockId, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            // 無料モードは所持数制限なし
            // Free mode has no held-count limit
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            // 設置可能セル分の不足素材を積む
            // Push short materials for the placeable cells
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var placeableCellCount = currentPlaceInfos.Count(info => info.Placeable);
            foreach (var shortage in ConstructionCostShortageCalculator.Calculate(blockMaster.RequiredItems, placeableCellCount, inventoryItems)) feedback.Add(ConstructionMaterialShortageLine.ToLine(shortage));

            // 建設コストで賄えるセル数まで設置可にする
            // Allow placement up to the affordable cell count
            var affordableCellCount = ConstructionCostPreviewCalculator.CalculateAffordableCellCount(blockMaster.RequiredItems, inventoryItems);
            var placeableCount = 0;
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                placeableCount++;
                if (affordableCellCount < placeableCount) currentPlaceInfos[i].Placeable = false;
            }
        }
    }
}
