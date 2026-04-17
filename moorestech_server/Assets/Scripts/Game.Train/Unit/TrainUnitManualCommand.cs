namespace Game.Train.Unit
{
    public readonly struct TrainUnitManualCommand
    {
        public readonly bool ReverseRequested;
        public readonly int MasconCommand;

        public TrainUnitManualCommand(bool reverseRequested, int masconCommand)
        {
            ReverseRequested = reverseRequested;
            MasconCommand = masconCommand;
        }
    }
}
