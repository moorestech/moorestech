using Core.Master;

namespace Game.Construction
{
    // 残り設置数の変更口
    // The write side of remaining placements; only the place/remove protocols depend on this
    public interface IRemainingPlacementCountMutation
    {
        // 設置1回分を進める。遷移式はConstructionWalletUtil.AdvanceOnPlacementが唯一の正本
        // Advances the wallet by one placement; ConstructionWalletUtil.AdvanceOnPlacement is the single source of the transition
        void ApplyPlacement(int playerId, BlockId walletBlockId, int placementsPerCost, ConstructionWalletUsage usage);

        // 撤去分を戻す。凝縮するかは財布が計画時に決める
        // Applies a removal's return; whether it condenses was decided by the wallet when the plan was made
        void ApplyReturn(int playerId, BlockId walletBlockId, bool condensed);

        // 溜めた変更を財布ごと1通の通知へ集約して吐き出す
        // Emits the accumulated changes as one notification per wallet
        void FlushChanges();
    }
}
