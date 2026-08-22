using System;
using System.Collections.Generic;
using Client.Game.InGame.Construction;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 建設コストで賄える設置セル数を算出
    /// Calculates how many placement cells the held materials can afford
    /// </summary>
    public static class ConstructionCostPreviewCalculator
    {
        public static int CalculateAffordableCellCount(ConstructionRequiredItemElement[] requiredItems, IEnumerable<IItemStack> inventoryItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return int.MaxValue;

            // 素材ごとの所持数からセル数の最小値を取る
            // Take the minimum affordable cells across materials
            var affordableCellCount = int.MaxValue;
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var total = 0;
                foreach (var stack in inventoryItems)
                {
                    if (stack.Id != itemId) continue;
                    total += stack.Count;
                }
                affordableCellCount = Math.Min(affordableCellCount, total / requiredItem.Count);
            }

            return affordableCellCount;
        }

        /// <summary>
        /// 残り設置数+買えるセット数×Nを返す
        /// Returns remaining + affordable sets × N
        /// </summary>
        public static int CalculateAffordablePlacementCount(ConstructionRequiredItemElement[] requiredItems, int placementsPerCost, int remainingCount, IEnumerable<IItemStack> inventoryItems)
        {
            // 設置数/1セット=1は財布を素通りするため、サーバー側4口と同じ全額課金の式に合わせる
            // placementsPerCost==1 bypasses the wallet; keep this in step with the server's four full-cost sites
            if (placementsPerCost <= 1) return CalculateAffordableCellCount(requiredItems, inventoryItems);

            var affordableSets = CalculateAffordableCellCount(requiredItems, inventoryItems);
            if (affordableSets == int.MaxValue) return int.MaxValue;

            // 大量所持でのオーバーフローを避ける
            // Avoid overflow on very large holdings
            var total = remainingCount + (long)affordableSets * placementsPerCost;
            return int.MaxValue < total ? int.MaxValue : (int)total;
        }

        /// <summary>
        /// 代表セルの財布・コストで賄えないセルをfalse化
        /// Marks cells beyond what the wallet and held materials can afford as Placeable=false
        /// </summary>
        public static void MarkUnaffordableCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockId representativeBlockId, ClientRemainingPlacementCountDatastore remainingPlacementCountDatastore, IEnumerable<IItemStack> inventoryItems)
        {
            // デバッグ中はコスト判定をスキップ
            // Skip cost checks during debug placement
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(representativeBlockId);
            var remaining = remainingPlacementCountDatastore.GetRemainingCount(ConstructionWalletUtil.ResolveWalletBlockId(representativeBlockId));
            var affordableCount = CalculateAffordablePlacementCount(blockMaster.RequiredItems, blockMaster.PlacementsPerCost, remaining, inventoryItems);

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
