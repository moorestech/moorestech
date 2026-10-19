namespace Client.Game.InGame.Train.Network
{
    // サーバーが1つの連番位置に載せた列車・レール・ベルトのハッシュ。間引きtickは3つともダミー
    // The train, rail and belt hashes the server put at one sequence position; a skipped tick carries the dummy for all three
    public readonly struct TrainTickHashState
    {
        public readonly uint UnitsHash;
        public readonly uint RailGraphHash;
        public readonly uint BeltTransportHash;
        public readonly uint ServerTick;
        public readonly uint TickSequenceId;

        public TrainTickHashState(uint unitsHash, uint railGraphHash, uint beltTransportHash, uint serverTick, uint tickSequenceId)
        {
            UnitsHash = unitsHash;
            RailGraphHash = railGraphHash;
            BeltTransportHash = beltTransportHash;
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
        }

        public bool IsDummy =>
            UnitsHash == TrainUnitFutureMessageBuffer.DummyHash &&
            RailGraphHash == TrainUnitFutureMessageBuffer.DummyHash &&
            BeltTransportHash == TrainUnitFutureMessageBuffer.DummyHash;
    }
}
