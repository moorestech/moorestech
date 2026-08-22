using Core.Master;

namespace Game.Construction
{
    // 残り設置数の変更通知。財布単位で最新値を運ぶ
    // Change notification for remaining placements, carrying the latest value per wallet
    public readonly struct RemainingPlacementCountChange
    {
        public readonly int PlayerId;
        public readonly BlockId WalletBlockId;
        public readonly int RemainingCount;

        internal RemainingPlacementCountChange(int playerId, BlockId walletBlockId, int remainingCount)
        {
            PlayerId = playerId;
            WalletBlockId = walletBlockId;
            RemainingCount = remainingCount;
        }
    }
}
