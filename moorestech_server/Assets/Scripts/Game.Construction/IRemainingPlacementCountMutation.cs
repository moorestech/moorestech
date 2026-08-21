using Core.Master;

namespace Game.Construction
{
    // 残り設置数の変更口。設置・撤去プロトコルだけがこちらへ依存する
    // The write side of remaining placements; only the place/remove protocols depend on this
    public interface IRemainingPlacementCountMutation
    {
        // 残り>0なら1消費してtrue
        // Consumes one when remaining>0 and returns true
        bool TryConsumeOne(int playerId, BlockId walletBlockId);

        // 建設コスト1セット消費の対価として設置数/1セット分を補充する
        // Refills one set's worth of placements after one construction-cost set was consumed
        void Refill(int playerId, BlockId walletBlockId, int placementsPerCost);

        // 撤去で1戻す。設置数/1セットに達したら0へ戻しtrue（呼び手が1セット返却する）
        // Returns one on removal; reaching placementsPerCost resets to zero and returns true (caller refunds one set)
        bool ReturnOne(int playerId, BlockId walletBlockId, int placementsPerCost);
    }
}
