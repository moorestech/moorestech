using Game.Train.RailPositions;
using Game.Train.Unit;
using System;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public enum TrainCarPlacementMode
    {
        CreateNewTrainUnit = 0,
        AttachToExistingTrainUnit = 1
    }

    public enum TrainCarAttachTargetEndpoint
    {
        Head = 0,
        Rear = 1
    }

    public readonly struct TrainCarPlacementHit
    {
        public TrainCarPlacementHit(
            bool isPlaceable,
            RailPosition railPosition,
            IReadOnlyList<TrainInstanceId> overlapTrainInstanceIds,
            TrainCarPlacementMode placementMode,
            TrainInstanceId targetTrainInstanceId,
            bool attachCarFacingForward,
            TrainCarAttachTargetEndpoint attachTargetEndpoint)
        {
            IsPlaceable = isPlaceable;
            RailPosition = railPosition;
            OverlapTrainInstanceIds = overlapTrainInstanceIds ?? Array.Empty<TrainInstanceId>();
            PlacementMode = placementMode;
            TargetTrainInstanceId = targetTrainInstanceId;
            AttachCarFacingForward = attachCarFacingForward;
            AttachTargetEndpoint = attachTargetEndpoint;
        }

        public bool IsPlaceable { get; }
        public RailPosition RailPosition { get; }
        public IReadOnlyList<TrainInstanceId> OverlapTrainInstanceIds { get; }
        public TrainCarPlacementMode PlacementMode { get; }
        public TrainInstanceId TargetTrainInstanceId { get; }
        public bool AttachCarFacingForward { get; }
        public TrainCarAttachTargetEndpoint AttachTargetEndpoint { get; }
    }
}
