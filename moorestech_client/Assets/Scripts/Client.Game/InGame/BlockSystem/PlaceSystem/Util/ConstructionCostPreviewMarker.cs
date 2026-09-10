using System.Collections.Generic;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 財布に置ける数を問い合わせ、超えたセルをfalse化
    /// Asks the wallet how many cells are placeable and marks the ones beyond that as Placeable=false
    /// </summary>
    public static class ConstructionCostPreviewMarker
    {
        // 列は張替えのように複数BlockIdが混ざるので、コストはセル自身のBlockIdごとに数える
        // A run can mix BlockIds (e.g. a replace run), so the cost is counted per the cell's own BlockId
        public static void MarkUnaffordableCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)
        {
            // デバッグ中はコスト判定をスキップ
            // Skip cost checks during debug placement
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            var affordableCounts = new Dictionary<BlockId, int>();
            var placeableCounts = new Dictionary<BlockId, int>();
            foreach (var placeInfo in currentPlaceInfos)
            {
                if (!placeInfo.Placeable) continue;

                var blockId = placeInfo.BlockId;
                if (!affordableCounts.TryGetValue(blockId, out var affordableCount))
                {
                    affordableCount = walletQuery.GetAffordablePlacementCount(blockId, inventoryItems);
                    affordableCounts.Add(blockId, affordableCount);
                    placeableCounts.Add(blockId, 0);
                }

                var placeableCount = placeableCounts[blockId] + 1;
                placeableCounts[blockId] = placeableCount;
                if (affordableCount < placeableCount) placeInfo.Placeable = false;
            }
        }
    }
}
