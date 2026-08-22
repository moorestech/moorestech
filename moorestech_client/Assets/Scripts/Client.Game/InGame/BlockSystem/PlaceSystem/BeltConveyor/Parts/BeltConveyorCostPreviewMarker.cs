using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 後続不足セルをPlaceable=falseに
    /// 不足素材をツールチップへ積む
    /// Marks cells beyond affordability as Placeable=false
    /// Pushes the short materials to the tooltip
    /// </summary>
    public static class BeltConveyorCostPreviewMarker
    {
        public static void MarkInsufficientEntitiesAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            // 無料設置デバッグ中はコストによる設置不可判定を行わない（CommonBlockPlaceSystemと同一のスキップ）
            // Skip cost-based unplaceable marking during free-placement debug (same skip as CommonBlockPlaceSystem)
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            // 地面埋没等の設置不可エンティティはコストを消費しないため予算計算から除外する
            // Exclude already-unplaceable entities (e.g. buried in ground) since they consume no cost
            var entityCosts = new List<ConstructionRequiredItemElement[]>(currentPlaceInfos.Count);
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                entityCosts.Add(MasterHolder.BlockMaster.GetBlockMaster(currentPlaceInfos[i].BlockId).RequiredItems);
            }

            // 今回置こうとしているエンティティ列ぶんの不足素材をツールチップへ積む
            // Push the materials short for the entities actually being placed
            feedback.AddMaterialShortages(ConstructionCostShortageCalculator.Calculate(entityCosts, inventoryItems));

            // 建設コストで賄えるエンティティ数まで設置可にする
            // Allow placement up to the affordable entity count
            var affordableEntityCount = ConstructionCostPreviewCalculator.CalculateAffordableEntityCount(entityCosts, inventoryItems);

            var placeableCount = 0;
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                placeableCount++;
                if (placeableCount > affordableEntityCount)
                {
                    currentPlaceInfos[i].Placeable = false;
                }
            }
        }
    }
}
