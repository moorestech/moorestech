using Game.Train.RailPositions;
using Game.Train.Unit;
using System;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public readonly struct TrainCarPlacementHit
    {
        public TrainCarPlacementHit(bool isPlaceable, RailPosition railPosition, IReadOnlyList<TrainInstanceId> overlapTrainInstanceIds)
        {
            IsPlaceable = isPlaceable;
            RailPosition = railPosition;
            OverlapTrainInstanceIds = overlapTrainInstanceIds ?? Array.Empty<TrainInstanceId>();
        }

        public bool IsPlaceable { get; }
        public RailPosition RailPosition { get; }
        public IReadOnlyList<TrainInstanceId> OverlapTrainInstanceIds { get; }
    }
}
