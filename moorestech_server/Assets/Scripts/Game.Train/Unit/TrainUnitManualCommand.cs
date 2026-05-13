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
        public static readonly TrainUnitManualCommand Default = new(false, TrainUnitMasconCommand.Neutral);
        public readonly bool ReverseRequested;
        public readonly TrainUnitMasconCommand MasconCommand;

        public TrainUnitManualCommand(bool reverseRequested, TrainUnitMasconCommand masconCommand)
        {
            ReverseRequested = reverseRequested;
            MasconCommand = masconCommand;
        }
    }
}
