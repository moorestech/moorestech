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
        IReadOnlyList<(BlockId walletBlockId, int remainingCount)> GetRemainingCounts(int playerId);

        // プレイヤーへ束縛済みの読み取り口を払い出す。財布の問い合わせはこの口から行う
        // Hands out a player-bound read port; every wallet query goes through it
        IRemainingPlacementCountReader GetReader(int playerId);
    }
}
