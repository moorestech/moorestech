namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     清浄機1台の稼働状況のスナップショット（UI表示用）
    ///     Snapshot of one air purifier's operating state for UI display
    /// </summary>
    public readonly struct CleanRoomAirFilterStatus
    {
        public readonly double PowerRatio;
        public readonly double RemovalVolumePerSecond;
        public readonly int FilterItemCount;

        // 装填中のフィルター1個の残り寿命（0〜1）。フィルターが無ければ0
        // Remaining life of the loaded filter (0 to 1); zero without a filter
        public readonly double CurrentFilterRemainingRatio;

        public CleanRoomAirFilterStatus(double powerRatio, double removalVolumePerSecond, int filterItemCount, double currentFilterRemainingRatio)
        {
            PowerRatio = powerRatio;
            RemovalVolumePerSecond = removalVolumePerSecond;
            FilterItemCount = filterItemCount;
            CurrentFilterRemainingRatio = currentFilterRemainingRatio;
        }
    }
}
