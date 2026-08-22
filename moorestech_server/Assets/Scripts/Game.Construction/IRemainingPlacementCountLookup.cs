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
        // 生のBlockIdを受け、財布キーへの正規化は実装側が行う（クライアント側と同一契約）
        // Takes a raw BlockId; normalizing it to the wallet key is the implementation's job, matching the client-side contract
        int GetRemainingCount(int playerId, BlockId blockId);
        IReadOnlyList<(BlockId walletBlockId, int remainingCount)> GetRemainingCounts(int playerId);
    }
}
