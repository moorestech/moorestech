namespace Game.Train.Unit
{
    public enum TrainUnitMasconCommand
    {
        Brake = -1,
        Neutral = 0,
        Accelerate = 1
    }

    public enum TrainUnitBranchCommand
    {
        Previous = -1,
        Neutral = 0,
        Next = 1
    }

    public readonly struct TrainUnitManualCommand
    {
        public static readonly TrainUnitManualCommand Default = new(false, TrainUnitMasconCommand.Neutral, TrainUnitBranchCommand.Neutral);
        public readonly bool ReverseRequested;
        public readonly TrainUnitMasconCommand MasconCommand;
        public readonly TrainUnitBranchCommand BranchCommand;

        public TrainUnitManualCommand(bool reverseRequested, TrainUnitMasconCommand masconCommand)
            : this(reverseRequested, masconCommand, TrainUnitBranchCommand.Neutral)
        {
        }

        public TrainUnitManualCommand(bool reverseRequested, TrainUnitMasconCommand masconCommand, TrainUnitBranchCommand branchCommand)
        {
            ReverseRequested = reverseRequested;
            MasconCommand = masconCommand;
            BranchCommand = branchCommand;
        }
    }
}
