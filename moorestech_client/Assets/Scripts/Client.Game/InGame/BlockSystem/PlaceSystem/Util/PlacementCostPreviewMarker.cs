using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 後続不足セルをPlaceable=falseに
    /// Marks cells beyond affordability as Placeable=false
    /// 不足素材をツールチップへ積む
    /// Pushes the short materials to the tooltip
    /// </summary>
    public static class PlacementCostPreviewMarker
    {
        public static void MarkInsufficientEntitiesAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            // 無料設置デバッグ中はコストによる設置不可判定を行わない
            // Skip cost-based unplaceable marking during free-placement debug
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            // 地面埋没等の設置不可セルはコストを消費しないため予算計算から除外する
            // Exclude already-unplaceable cells (e.g. buried in ground) since they consume no cost
            var entityCosts = new List<ConstructionRequiredItemElement[]>(currentPlaceInfos.Count);
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                entityCosts.Add(MasterHolder.BlockMaster.GetBlockMaster(currentPlaceInfos[i].BlockId).RequiredItems);
            }

            // 今回置こうとしているセル列ぶんの不足素材をツールチップへ積む
            // Push the materials short for the cells actually being placed
            foreach (var shortage in ConstructionCostShortageCalculator.Calculate(entityCosts, inventoryItems)) feedback.Add(ConstructionMaterialShortageLine.ToLine(shortage));

            // 建設コストで賄えるセル数まで設置可にする
            // Allow placement up to the affordable cell count
            var affordableEntityCount = ConstructionCostPreviewCalculator.CalculateAffordableEntityCount(entityCosts, inventoryItems);

            var placeableCount = 0;
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                placeableCount++;
                if (affordableEntityCount < placeableCount) currentPlaceInfos[i].Placeable = false;
            }
        }
    }
}
