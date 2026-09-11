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

            // 賄える数と数え上げた設置数は必ず同時に生まれるので1本の辞書へ束ねる（片方だけ足す変更で崩れないようにする）
            // The affordable count and the running placed count always appear together, so one dictionary holds both
            var countsByBlockId = new Dictionary<BlockId, (int affordable, int placed)>();
            foreach (var placeInfo in currentPlaceInfos)
            {
                if (!placeInfo.Placeable) continue;

                var blockId = placeInfo.BlockId;
                if (!countsByBlockId.TryGetValue(blockId, out var counts)) counts = (walletQuery.GetAffordablePlacementCount(blockId, inventoryItems), 0);

                counts.placed++;
                countsByBlockId[blockId] = counts;
                if (counts.affordable < counts.placed) placeInfo.Placeable = false;
            }
        }
    }
}
