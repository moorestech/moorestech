using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
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

        public ConstructionWalletService(IRemainingPlacementCountLookup lookup, IRemainingPlacementCountMutation mutation)
        {
            _lookup = lookup;
            _mutation = mutation;
        }

        // 問い合わせ後、確定でCommitPlacementを呼ぶ
        // Ask, then call CommitPlacement once final
        public IConstructionPlacementPlan PlanPlacement(BlockMasterElement blockMaster, int playerId)
        {
            var fullCost = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (blockMaster.PlacementsPerCost <= 1) return new PlacementPlan(fullCost);

            // 残りありは素材消費せず財布から1引く
            // A cell covered by the wallet draws one from the wallet, no materials consumed
            var walletBlockId = ResolveWalletBlockId(blockMaster);
            var covered = 0 < _lookup.GetRemainingCount(playerId, walletBlockId);
            return new PlacementPlan(covered ? Array.Empty<(ItemId, int)>() : fullCost, playerId, walletBlockId, blockMaster.PlacementsPerCost, !covered);
        }

        // 設置確定後にのみ呼ぶ。呼ばなければ財布も素材も変わらない
        // Call only after placement is confirmed; skipping it leaves both wallet and materials untouched
        public void CommitPlacement(IConstructionPlacementPlan plan, IOpenableInventory inventory)
        {
            var placement = (PlacementPlan)plan;
            ConstructionCostService.ConsumeRequiredItems(placement.ItemsToConsume, inventory);
            if (!placement.UsesWallet) return;

            // 素材を払ったセルは1セット分を補充してから1消費する（残り=N-1）
            // A cell that paid materials refills one set's worth and then consumes one (remaining = N-1)
            if (placement.RefillsWallet) _mutation.Refill(placement.PlayerId, placement.WalletBlockId, placement.PlacementsPerCost);
            _mutation.TryConsumeOne(placement.PlayerId, placement.WalletBlockId);
        }

        // 問い合わせ後、確定でCommitRemovalを呼ぶ
        // Ask, then call CommitRemoval once final
        public IConstructionRemovalPlan PlanRemoval(BlockMasterElement blockMaster, int playerId)
        {
            var fullCost = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (blockMaster.PlacementsPerCost <= 1) return new RemovalPlan(ConstructionCostService.CreateRefundItems(fullCost));

            // 1セット分が貯まる撤去でだけ素材が戻る
            // Materials come back only on the removal that completes one set's worth
            var walletBlockId = ResolveWalletBlockId(blockMaster);
            var condenses = ConstructionWalletUtil.WouldCondense(_lookup.GetRemainingCount(playerId, walletBlockId), blockMaster.PlacementsPerCost);
            IReadOnlyList<IItemStack> refund = condenses ? ConstructionCostService.CreateRefundItems(fullCost) : (IReadOnlyList<IItemStack>)Array.Empty<IItemStack>();
            return new RemovalPlan(refund, playerId, walletBlockId, blockMaster.PlacementsPerCost);
        }

        // 撤去確定後にのみ呼ぶ。返却物は Plan の時点で確保済み
        // Call only after removal is final; the refund was already reserved when the plan was made
        public void CommitRemoval(IConstructionRemovalPlan plan)
        {
            var removal = (RemovalPlan)plan;
            if (!removal.UsesWallet) return;
            _mutation.ReturnOne(removal.PlayerId, removal.WalletBlockId, removal.PlacementsPerCost);
        }

        // 財布キーの解決はここだけが持つ（呼び出し側は財布キーの存在すら意識しない）
        // Wallet-key resolution lives only here, so no caller ever has to know it exists
        private static BlockId ResolveWalletBlockId(BlockMasterElement blockMaster)
        {
            return ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
        }

        // 財布の内訳は private 入れ子に閉じ、外へは interface の指示だけが出る
        // The wallet bookkeeping stays in private nested types; only the interface instruction leaves this class
        private class PlacementPlan : IConstructionPlacementPlan
        {
            public IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume { get; }
            internal bool UsesWallet { get; }
            internal bool RefillsWallet { get; }
            internal int PlayerId { get; }
            internal BlockId WalletBlockId { get; }
            internal int PlacementsPerCost { get; }

            internal PlacementPlan(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume)
            {
                ItemsToConsume = itemsToConsume;
            }

            internal PlacementPlan(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume, int playerId, BlockId walletBlockId, int placementsPerCost, bool refillsWallet)
            {
                ItemsToConsume = itemsToConsume;
                UsesWallet = true;
                RefillsWallet = refillsWallet;
                PlayerId = playerId;
                WalletBlockId = walletBlockId;
                PlacementsPerCost = placementsPerCost;
            }
        }

        private class RemovalPlan : IConstructionRemovalPlan
        {
            public IReadOnlyList<IItemStack> ItemsToRefund { get; }
            internal bool UsesWallet { get; }
            internal int PlayerId { get; }
            internal BlockId WalletBlockId { get; }
            internal int PlacementsPerCost { get; }

            internal RemovalPlan(IReadOnlyList<IItemStack> itemsToRefund)
            {
                ItemsToRefund = itemsToRefund;
            }

            internal RemovalPlan(IReadOnlyList<IItemStack> itemsToRefund, int playerId, BlockId walletBlockId, int placementsPerCost)
            {
                ItemsToRefund = itemsToRefund;
                UsesWallet = true;
                PlayerId = playerId;
                WalletBlockId = walletBlockId;
                PlacementsPerCost = placementsPerCost;
            }
        }
    }
}
