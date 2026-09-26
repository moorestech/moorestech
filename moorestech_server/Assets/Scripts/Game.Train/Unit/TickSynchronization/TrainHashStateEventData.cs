namespace Game.Train.Unit
{
    public readonly struct HashStateEventData
    {
        public uint Tick { get; }
        public uint UnitsHash { get; }
        public uint RailGraphHash { get; }

        public HashStateEventData(uint tick, uint unitsHash, uint railGraphHash)
        {
            Tick = tick;
            UnitsHash = unitsHash;
            RailGraphHash = railGraphHash;
        }
    }
}
