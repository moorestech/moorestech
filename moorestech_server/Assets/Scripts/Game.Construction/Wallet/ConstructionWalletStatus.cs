namespace Game.Construction
{
    // 表示用の財布の状態。財布を使わないブロックには存在しない
    // The wallet state for display; blocks that bypass the wallet have none
    public readonly struct ConstructionWalletStatus
    {
        public readonly int PlacementsPerCost;
        public readonly int RemainingCount;

        public ConstructionWalletStatus(int placementsPerCost, int remainingCount)
        {
            PlacementsPerCost = placementsPerCost;
            RemainingCount = remainingCount;
        }

        // 次の1セルを残りで賄えるか。残数の解釈は呼び出し元に作らせない
        // Whether the remainder covers the next cell; callers never reinterpret the raw count
        public bool CoversNextPlacement()
        {
            return ConstructionWalletUtil.IsCoveredByWallet(RemainingCount);
        }
    }
}
