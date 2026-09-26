namespace Game.Train.Unit
{
    public readonly struct TrainHashStateEventData
    {
        public uint Tick { get; }
        public uint UnitsHash { get; }
        public uint RailGraphHash { get; }

        public TrainHashStateEventData(uint tick, uint unitsHash, uint railGraphHash)
        {
            Tick = tick;
            UnitsHash = unitsHash;
            RailGraphHash = railGraphHash;
        }
    }
}
