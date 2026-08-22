using Core.Master;

namespace Game.Construction
{
    // 残り設置数の変更口
    // The write side of remaining placements; only the place/remove protocols depend on this
    public interface IRemainingPlacementCountMutation
    {
        // 残り>0なら1消費してtrue
        // Consumes one when remaining>0 and returns true
        bool TryConsumeOne(int playerId, BlockId walletBlockId);

        // 1セット消費の対価としてN分を補充する
        // Refills one set's worth of placements after one construction-cost set was consumed
        void Refill(int playerId, BlockId walletBlockId, int placementsPerCost);

        // 撤去で+1、N到達で0にしtrue(要1セット返却)
        // Returns one on removal; reaching placementsPerCost resets to zero and returns true (caller refunds one set)
        bool ReturnOne(int playerId, BlockId walletBlockId, int placementsPerCost);
    }
}
