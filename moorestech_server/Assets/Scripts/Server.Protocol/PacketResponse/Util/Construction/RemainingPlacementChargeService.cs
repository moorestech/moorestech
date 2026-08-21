using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Construction;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 残り設置数の財布を見て、このセルで実際に消費する建設コストを決め、設置・撤去で財布と素材を更新する（ADR 0026）
    /// Decides the construction cost actually consumed for a cell from the remaining-placement wallet, then updates wallet and materials on both placement and removal (ADR 0026)
    /// </summary>
    public static class RemainingPlacementChargeService
    {
        // 設置数/1セット=1は財布を素通りし全額消費、財布に残りがあれば消費ゼロ
        // placementsPerCost==1 bypasses the wallet and consumes the full cost; a non-empty wallet consumes nothing
        public static (ItemId itemId, int count)[] ResolveCostToConsume(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountLookup lookup)
        {
            var fullCost = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (blockMaster.PlacementsPerCost <= 1 || fullCost.Length == 0) return fullCost;

            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            return 0 < lookup.GetRemainingCount(playerId, walletBlockId) ? Array.Empty<(ItemId, int)>() : fullCost;
        }

        // 設置確定後にのみ呼ぶこと。TryAddBlock失敗時は呼ばず財布・素材とも変えない
        // Call only after placement is confirmed; skip on TryAddBlock failure so neither the wallet nor materials change
        public static void Charge(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountMutation mutation, IReadOnlyList<(ItemId itemId, int count)> costToConsume, IOpenableInventory inventory)
        {
            ConstructionCostService.ConsumeRequiredItems(costToConsume, inventory);
            if (blockMaster.PlacementsPerCost <= 1) return;

            // 素材を消費したセルは設置数/1セット分を補充してから1消費する（残り=N-1）
            // A cell that consumed materials refills one set's worth and then consumes one (remaining = N-1)
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            if (0 < costToConsume.Count) mutation.Refill(playerId, walletBlockId, blockMaster.PlacementsPerCost);
            mutation.TryConsumeOne(playerId, walletBlockId);
        }

        // 撤去がこのセルで素材1セットの返却になるか。設置数/1セット=1は常に全額返却
        // Whether this removal refunds one set of materials; placementsPerCost==1 always refunds in full
        public static bool WouldCondenseOnReturn(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountLookup lookup)
        {
            if (blockMaster.PlacementsPerCost <= 1) return true;

            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            return blockMaster.PlacementsPerCost <= lookup.GetRemainingCount(playerId, walletBlockId) + 1;
        }

        // 撤去確定後にのみ呼ぶこと。設置数/1セット=1は財布を素通りする
        // Call only after removal is final; placementsPerCost==1 bypasses the wallet
        public static void ReturnOne(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountMutation mutation)
        {
            if (blockMaster.PlacementsPerCost <= 1) return;

            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            mutation.ReturnOne(playerId, walletBlockId, blockMaster.PlacementsPerCost);
        }
    }
}
