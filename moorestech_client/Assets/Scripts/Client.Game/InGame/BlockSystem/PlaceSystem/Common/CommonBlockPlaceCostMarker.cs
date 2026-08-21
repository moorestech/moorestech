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
    /// 通常設置のセル列のうち所持素材で賄えない後続分をPlaceable=falseへ書き換え、不足素材をツールチップへ積む
    /// Marks normal-placement cells beyond what the held materials can afford as Placeable=false and pushes the short materials to the tooltip
    /// </summary>
    public static class CommonBlockPlaceCostMarker
    {
        public static void MarkInsufficientCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockId blockId, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            // 無料設置モードでは所持数による制限をかけない
            // In free placement mode, do not limit by held item count
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            // 今回置こうとしている（地形・重複で落ちていない）セル数ぶんの不足素材をツールチップへ積む
            // Push the materials short for the cells actually being placed (not dropped by terrain/overlap)
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var placeableCellCount = currentPlaceInfos.Count(info => info.Placeable);
            feedback.AddMaterialShortages(ConstructionCostShortageCalculator.Calculate(blockMaster.RequiredItems, placeableCellCount, inventoryItems));

            // 建設コストで賄えるセル数まで設置可にする
            // Allow placement up to the affordable cell count
            var affordableCellCount = ConstructionCostPreviewCalculator.CalculateAffordableCellCount(blockMaster.RequiredItems, inventoryItems);
            var placeableCount = 0;
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                placeableCount++;
                if (placeableCount > affordableCellCount) currentPlaceInfos[i].Placeable = false;
            }
        }
    }
}
