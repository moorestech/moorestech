using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using UniRx;

namespace Game.Construction
{
    /// <summary>
    /// 財布への問い合わせ窓口。何を消費するか・何セル置けるか・表示状態を答え、判断は内側に閉じる
    /// The wallet's query window; it answers what to consume, how many cells fit, and the display state, keeping every decision inside
    /// </summary>
    public class ConstructionWalletQuery
    {
        public IObservable<Unit> OnWalletChanged => _reader.OnWalletChanged;

        private readonly IRemainingPlacementCountReader _reader;

        public ConstructionWalletQuery(IRemainingPlacementCountReader reader)
        {
            _reader = reader;
        }

        // 表示用の財布状態。財布を通らないブロックはnullで「財布は無い」を表す
        // The wallet state for display; blocks that bypass the wallet return null to say there is no wallet
        public ConstructionWalletStatus? GetWalletStatus(BlockId blockId)
        {
            var placementsPerCost = MasterHolder.BlockMaster.GetBlockMaster(blockId).PlacementsPerCost;
            if (!ConstructionWalletUtil.UsesWallet(placementsPerCost)) return null;
            return new ConstructionWalletStatus(placementsPerCost, _reader.GetRemainingCount(blockId));
        }

        // このセルを残りで賄えるか。財布を通らないブロックは常にfalse
        // Whether the remainder covers this cell; blocks that bypass the wallet are always false
        public bool IsCoveredByWallet(BlockId blockId)
        {
            var placementsPerCost = MasterHolder.BlockMaster.GetBlockMaster(blockId).PlacementsPerCost;
            if (!ConstructionWalletUtil.UsesWallet(placementsPerCost)) return false;
            return ConstructionWalletUtil.IsCoveredByWallet(_reader.GetRemainingCount(blockId));
        }

        // このセルを置くと実際に消費する素材。残りで賄うなら空
        // The materials this cell actually consumes; empty when the remainder covers it
        public IReadOnlyList<(ItemId itemId, int count)> GetItemsToConsume(BlockId blockId)
        {
            if (IsCoveredByWallet(blockId)) return Array.Empty<(ItemId, int)>();
            return ConstructionCostItems.ToItemCounts(MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems);
        }

        // 表示中のセル数に対し実際に払うコストセット数
        // The cost sets actually paid for the cells being previewed
        public int GetRequiredCostSets(BlockId blockId, int cellCount)
        {
            var placementsPerCost = MasterHolder.BlockMaster.GetBlockMaster(blockId).PlacementsPerCost;
            return ConstructionWalletUtil.CalculateRequiredCostSets(_reader.GetRemainingCount(blockId), cellCount, placementsPerCost);
        }

        // 残りと所持素材で何セル置けるか
        // How many cells the remainder plus the held materials can cover
        public int GetAffordablePlacementCount(BlockId blockId, IEnumerable<IItemStack> inventoryItems)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var affordableSets = ConstructionMaterialAffordability.CalculateAffordableCellCount(blockMaster.RequiredItems, inventoryItems);
            return ConstructionWalletUtil.CalculatePlaceableCount(_reader.GetRemainingCount(blockId), affordableSets, blockMaster.PlacementsPerCost);
        }
    }
}
