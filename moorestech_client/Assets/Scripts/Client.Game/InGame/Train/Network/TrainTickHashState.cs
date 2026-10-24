namespace Client.Game.InGame.Train.Network
{
    // サーバーが1つの連番位置に載せた列車・レール・ベルト・流体のハッシュ。間引きtickは4つともダミー
    // The train, rail, belt and fluid hashes the server put at one sequence position; a skipped tick carries the dummy for all four
    public readonly struct TrainTickHashState
    {
        public readonly uint UnitsHash;
        public readonly uint RailGraphHash;
        public readonly uint BeltTransportHash;
        public readonly uint FluidNetworkHash;
        public readonly uint ServerTick;
        public readonly uint TickSequenceId;

        public TrainTickHashState(uint unitsHash, uint railGraphHash, uint beltTransportHash, uint fluidNetworkHash, uint serverTick, uint tickSequenceId)
        {
            UnitsHash = unitsHash;
            RailGraphHash = railGraphHash;
            BeltTransportHash = beltTransportHash;
            FluidNetworkHash = fluidNetworkHash;
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
        }

        public bool IsDummy =>
            UnitsHash == TrainUnitFutureMessageBuffer.DummyHash &&
            RailGraphHash == TrainUnitFutureMessageBuffer.DummyHash &&
            BeltTransportHash == TrainUnitFutureMessageBuffer.DummyHash &&
            FluidNetworkHash == TrainUnitFutureMessageBuffer.DummyHash;
    }
}
