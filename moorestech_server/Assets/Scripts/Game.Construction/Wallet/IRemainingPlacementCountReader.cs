using System;
using Core.Master;
using UniRx;

namespace Game.Construction
{
    // 財布1つ分（プレイヤー1人分）の読み取り口。プレイヤーの束縛は実装側が済ませる
    // The read side of one wallet holder's remaining placements; binding to a player is the implementation's job
    public interface IRemainingPlacementCountReader
    {
        // 財布が動いたことだけを知らせる。何がどう動いたかは問い合わせ直す
        // Signals only that a wallet moved; what changed is re-queried
        IObservable<Unit> OnWalletChanged { get; }

        // 生のBlockIdを受け、財布キーへの正規化は実装側が行う
        // Takes a raw BlockId; normalizing it to the wallet key is the implementation's job
        int GetRemainingCount(BlockId blockId);
    }
}
