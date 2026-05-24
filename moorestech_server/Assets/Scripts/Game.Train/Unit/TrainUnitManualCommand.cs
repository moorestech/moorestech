namespace Game.Train.Unit
{
    public enum TrainUnitMasconCommand
    {
        Brake = -1,
        Neutral = 0,
        Accelerate = 1
    }

    public readonly struct TrainUnitManualCommand
    {
        public static readonly TrainUnitManualCommand Default = new(false, TrainUnitMasconCommand.Neutral, 0);
        public readonly bool ReverseRequested;
        public readonly TrainUnitMasconCommand MasconCommand;
        public readonly int BranchSelectionIndexDelta;

        public TrainUnitManualCommand(bool reverseRequested, TrainUnitMasconCommand masconCommand)
            : this(reverseRequested, masconCommand, 0)
        {
        }

        public TrainUnitManualCommand(bool reverseRequested, TrainUnitMasconCommand masconCommand, int branchSelectionIndexDelta)
        {
            ReverseRequested = reverseRequested;
            MasconCommand = masconCommand;
            BranchSelectionIndexDelta = branchSelectionIndexDelta;
        }
    }
}
