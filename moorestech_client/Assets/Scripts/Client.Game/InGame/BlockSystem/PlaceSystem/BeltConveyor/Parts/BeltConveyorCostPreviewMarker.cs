using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Construction;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ベルトセル列のうち所持素材で賄えない後続分をPlaceable=falseへ書き換える
    /// Marks belt cells beyond what the held materials can afford as Placeable=false
    /// </summary>
    public static class BeltConveyorCostPreviewMarker
    {
        public static void MarkInsufficientEntitiesAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, IEnumerable<IItemStack> inventoryItems, ClientRemainingPlacementCountDatastore remainingPlacementCountDatastore)
        {
            // 無料設置デバッグ中はコストによる設置不可判定を行わない（CommonBlockPlaceSystemと同一のスキップ）
            // Skip cost-based unplaceable marking during free-placement debug (same skip as CommonBlockPlaceSystem)
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            // ファミリー内は建設コストと設置数/1セットが一致する（マスタ検証済み）ので先頭の設置可セルを代表にする
            // Cost and placementsPerCost match within a family (validated at master load), so the first placeable cell is representative
            var representativeIndex = currentPlaceInfos.FindIndex(info => info.Placeable);
            if (representativeIndex < 0) return;
            var blockId = currentPlaceInfos[representativeIndex].BlockId;
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var remaining = remainingPlacementCountDatastore.GetRemainingCount(ConstructionWalletUtil.ResolveWalletBlockId(blockId));
            var affordableCount = ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(blockMaster.RequiredItems, blockMaster.PlacementsPerCost, remaining, inventoryItems);

            var placeableCount = 0;
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                placeableCount++;
                if (placeableCount > affordableCount)
                {
                    currentPlaceInfos[i].Placeable = false;
                }
            }
        }
    }
}
