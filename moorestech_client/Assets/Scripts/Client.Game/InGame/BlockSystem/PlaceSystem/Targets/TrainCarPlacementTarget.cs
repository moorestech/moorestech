using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class TrainCarPlacementTarget : IPlacementTarget
    {
        public readonly Guid TrainCarGuid;

        public TrainCarPlacementTarget(Guid trainCarGuid)
        {
            TrainCarGuid = trainCarGuid;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is TrainCarPlacementTarget target && TrainCarGuid == target.TrainCarGuid;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => TrainCarGuid.GetHashCode();
    }
}
