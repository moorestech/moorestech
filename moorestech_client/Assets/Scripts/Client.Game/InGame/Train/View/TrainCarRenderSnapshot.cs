using Game.Train.RailPositions;

namespace Client.Game.InGame.Train.View
{
    public readonly struct TrainCarRenderSnapshot
    {
        public uint Tick { get; }
        public RailPosition RailPosition { get; }
        public int FrontOffset { get; }
        public int RearOffset { get; }
        public bool IsFacingForward { get; }
        public double CurrentSpeed { get; }
        public int MasconLevel { get; }

        private TrainCarRenderSnapshot(uint tick, RailPosition railPosition, int frontOffset, int rearOffset, bool isFacingForward, double currentSpeed, int masconLevel)
        {
            Tick = tick;
            RailPosition = railPosition;
            FrontOffset = frontOffset;
            RearOffset = rearOffset;
            IsFacingForward = isFacingForward;
            CurrentSpeed = currentSpeed;
            MasconLevel = masconLevel;
        }

        public static TrainCarRenderSnapshot Create(uint tick, RailPosition railPosition, int frontOffset, int rearOffset, bool isFacingForward, double currentSpeed, int masconLevel)
        {
            return new TrainCarRenderSnapshot(tick, railPosition, frontOffset, rearOffset, isFacingForward, currentSpeed, masconLevel);
        }
    }
}
