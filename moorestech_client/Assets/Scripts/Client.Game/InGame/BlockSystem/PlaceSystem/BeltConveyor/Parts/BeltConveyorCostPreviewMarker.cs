using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 長尺分解済みエンティティ列に対し、所持素材で賄えない後続分をPlaceable=falseへ書き換える
    /// Marks decomposed entities beyond what the held materials can afford as Placeable=false
    /// </summary>
    public static class BeltConveyorCostPreviewMarker
    {
        public static void MarkInsufficientEntitiesAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, IEnumerable<IItemStack> inventoryItems)
        {
            var entityCosts = new ConstructionRequiredItemElement[currentPlaceInfos.Count][];
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                entityCosts[i] = MasterHolder.BlockMaster.GetBlockMaster(currentPlaceInfos[i].BlockId).RequiredItems;
            }

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
