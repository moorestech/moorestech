using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 残り設置数の財布。設置・撤去とも「何を消費/返却するか」の指示だけを返し、判断を内側に閉じる
    /// The remaining-placement wallet; placement and removal both hand back an instruction saying what to consume or refund, keeping every decision inside
    /// </summary>
    public class ConstructionWalletService
    {
        private readonly IRemainingPlacementCountLookup _lookup;
        private readonly IRemainingPlacementCountMutation _mutation;
        private readonly ConstructionPayerDataStore _payers;

        public ConstructionWalletService(IRemainingPlacementCountLookup lookup, IRemainingPlacementCountMutation mutation, ConstructionPayerDataStore payers)
        {
            _lookup = lookup;
            _mutation = mutation;
            _payers = payers;
        }

        // 問い合わせ後、確定でCommitPlacementを呼ぶ
        // Ask, then call CommitPlacement once final
        public IConstructionPlacementPlan PlanPlacement(BlockMasterElement blockMaster, int playerId)
        {
            var fullCost = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (!ConstructionWalletUtil.UsesWallet(blockMaster.PlacementsPerCost)) return new DirectCostPlacementPlan(fullCost);

            // 残りありは素材消費せず財布から1引く
            // A cell covered by the wallet draws one from the wallet, no materials consumed
            var walletBlockId = ResolveWalletBlockId(blockMaster);
            var covered = ConstructionWalletUtil.IsCoveredByWallet(_lookup.GetRemainingCount(playerId, walletBlockId));
            var usage = covered ? ConstructionWalletUsage.CoveredByWallet : ConstructionWalletUsage.PaidAndRefilled;
            var itemsToConsume = covered ? Array.Empty<(ItemId, int)>() : fullCost;
            return new WalletPlacementPlan(itemsToConsume, _mutation, _payers, usage, playerId, walletBlockId, blockMaster.PlacementsPerCost);
        }

        public void CommitPlacement(IConstructionPlacementPlan plan, IOpenableInventory inventory, BlockInstanceId blockInstanceId)
        {
            plan.Commit(inventory, blockInstanceId);
        }

        // 問い合わせ後、確定でCommitRemovalを呼ぶ
        // Ask, then call CommitRemoval once final
        public IConstructionRemovalPlan PlanRemoval(BlockMasterElement blockMaster, BlockInstanceId blockInstanceId, int removePlayerId)
        {
            var fullCost = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (!ConstructionWalletUtil.UsesWallet(blockMaster.PlacementsPerCost)) return new DirectCostRemovalPlan(ConstructionCostService.CreateRefundItems(fullCost));

            // 戻し先は撤去した人ではなく設置して支払った人の財布
            // The remainder goes back to whoever placed and paid for the block, not to whoever removes it
            var payerPlayerId = _payers.GetPayer(blockInstanceId, removePlayerId);

            // 1セット分が貯まる撤去でだけ素材が戻る
            // Materials come back only on the removal that completes one set's worth
            var walletBlockId = ResolveWalletBlockId(blockMaster);
            var condensed = ConstructionWalletUtil.WouldCondense(_lookup.GetRemainingCount(payerPlayerId, walletBlockId), blockMaster.PlacementsPerCost);
            IReadOnlyList<IItemStack> refund = condensed ? ConstructionCostService.CreateRefundItems(fullCost) : Array.Empty<IItemStack>();
            return new WalletRemovalPlan(refund, _mutation, _payers, payerPlayerId, walletBlockId, blockInstanceId, condensed);
        }

        public void CommitRemoval(IConstructionRemovalPlan plan)
        {
            plan.Commit();
        }

        // 設置・撤去1操作の末尾で呼び、溜まった残り設置数の変更を財布ごと1通へ集約する
        // Called at the end of one place/remove operation to collapse the accumulated changes into one notification per wallet
        public void FlushRemainingCountChanges()
        {
            _mutation.FlushChanges();
        }

        // 財布キーの解決はここだけが持つ（呼び出し側は財布キーの存在すら意識しない）
        // Wallet-key resolution lives only here, so no caller ever has to know it exists
        private static BlockId ResolveWalletBlockId(BlockMasterElement blockMaster)
        {
            return ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
        }
    }
}
