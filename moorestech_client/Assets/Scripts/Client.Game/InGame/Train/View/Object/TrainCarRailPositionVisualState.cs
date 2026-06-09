using Game.Train.RailPositions;

namespace Client.Game.InGame.Train.View.Object
{
    public readonly struct TrainCarRailPositionVisualState
    {
        public readonly RailPosition RailPosition;
        public readonly int FrontOffset;
        public readonly int RearOffset;
        public readonly bool IsFacingForward;

        private TrainCarRailPositionVisualState(RailPosition railPosition, int frontOffset, int rearOffset, bool isFacingForward)
        {
            RailPosition = railPosition;
            FrontOffset = frontOffset;
            RearOffset = rearOffset;
            IsFacingForward = isFacingForward;
        }

        public static TrainCarRailPositionVisualState Create(RailPosition railPosition, int frontOffset, int rearOffset, bool isFacingForward)
        {
            return new TrainCarRailPositionVisualState(railPosition, frontOffset, rearOffset, isFacingForward);
        }

    }
}
