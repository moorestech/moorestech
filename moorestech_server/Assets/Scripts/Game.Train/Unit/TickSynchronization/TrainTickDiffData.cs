namespace Game.Train.Unit
{
    public readonly struct TrainTickDiffData
    {
        public TrainUnitInstanceId TrainUnitInstanceId { get; }
        public int MasconLevelDiff { get; }
        public bool IsNowDockingSpeedZero { get; }
        public int ApproachingNodeIdDiff { get; }
        public bool IsReversedThisTick { get; }
        public int ManualBranchSelectionIndexDiff { get; }

        public TrainTickDiffData(TrainUnitInstanceId trainUnitInstanceId, int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff, bool isReversedThisTick, int manualBranchSelectionIndexDiff)
        {
            TrainUnitInstanceId = trainUnitInstanceId;
            MasconLevelDiff = masconLevelDiff;
            IsNowDockingSpeedZero = isNowDockingSpeedZero;
            ApproachingNodeIdDiff = approachingNodeIdDiff;
            IsReversedThisTick = isReversedThisTick;
            ManualBranchSelectionIndexDiff = manualBranchSelectionIndexDiff;
        }
    }
}
