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
    }
}
