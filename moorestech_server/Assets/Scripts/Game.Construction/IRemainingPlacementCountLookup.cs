using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Construction
{
    // 残り設置数の読み取り口
    // The read side of remaining placements; publishers, initial-data bundlers, and refund checks depend on this
    public interface IRemainingPlacementCountLookup
    {
        IObservable<RemainingPlacementCountChange> OnRemainingCountChanged { get; }
        int GetRemainingCount(int playerId, BlockId walletBlockId);
        IReadOnlyList<(BlockId walletBlockId, int remainingCount)> GetRemainingCounts(int playerId);
    }
}
