using System.Collections.Generic;
using Client.Game.InGame.Construction;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 財布に置ける数を問い合わせ、超えたセルをfalse化
    /// Asks the wallet how many cells are placeable and marks the ones beyond that as Placeable=false
    /// </summary>
    public static class ConstructionCostPreviewMarker
    {
        public static void MarkUnaffordableCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockId representativeBlockId, ClientRemainingPlacementCountDatastore remainingPlacementCountDatastore, IEnumerable<IItemStack> inventoryItems)
        {
            // デバッグ中はコスト判定をスキップ
            // Skip cost checks during debug placement
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            var affordableCount = remainingPlacementCountDatastore.GetAffordablePlacementCount(representativeBlockId, inventoryItems);

            var placeableCount = 0;
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                placeableCount++;
                if (affordableCount < placeableCount)
                {
                    currentPlaceInfos[i].Placeable = false;
                }
            }
        }
    }
}
