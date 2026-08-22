using Core.Master;

namespace Game.Construction
{
    // 残り設置数の変更口
    // The write side of remaining placements; only the place/remove protocols depend on this
    public interface IRemainingPlacementCountMutation
    {
        // 1消費する。残り0での呼び出しは財布の判断漏れなので落とす
        // Consumes one; calling it on an empty wallet means the caller skipped the wallet's decision, so it throws
        void ConsumeOne(int playerId, BlockId walletBlockId);

        // 1セット消費の対価としてN分を補充する
        // Refills one set's worth of placements after one construction-cost set was consumed
        void Refill(int playerId, BlockId walletBlockId, int placementsPerCost);

        // 撤去分を戻す。凝縮するかは財布が計画時に決める
        // Applies a removal's return; whether it condenses was decided by the wallet when the plan was made
        void ApplyReturn(int playerId, BlockId walletBlockId, bool condensed);

        // 溜めた変更を財布ごと1通の通知へ集約して吐き出す
        // Emits the accumulated changes as one notification per wallet
        void FlushChanges();
    }
}
