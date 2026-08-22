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
            RailPosition railPosition,
            IReadOnlyList<TrainUnitInstanceId> overlapTrainUnitInstanceIds,
            TrainCarPlacementMode placementMode,
            TrainUnitInstanceId targetTrainUnitInstanceId,
            bool attachCarFacingForward,
            TrainCarAttachTargetEndpoint attachTargetEndpoint,
            TrainCarPlacementBlockReason blockReason)
        {
            RailPosition = railPosition;
            OverlapTrainUnitInstanceIds = overlapTrainUnitInstanceIds ?? Array.Empty<TrainUnitInstanceId>();
            PlacementMode = placementMode;
            TargetTrainUnitInstanceId = targetTrainUnitInstanceId;
            AttachCarFacingForward = attachCarFacingForward;
            AttachTargetEndpoint = attachTargetEndpoint;
            BlockReason = blockReason;
        }

        // 可否は理由から導出する（「不可なのに理由None」「可なのに理由あり」を表現不能にする）
        // Placeability is derived from the reason, so "blocked with no reason" and "placeable with a reason" cannot be expressed
        public bool IsPlaceable => BlockReason == TrainCarPlacementBlockReason.None;
        public RailPosition RailPosition { get; }
        public IReadOnlyList<TrainUnitInstanceId> OverlapTrainUnitInstanceIds { get; }
        public TrainCarPlacementMode PlacementMode { get; }
        public TrainUnitInstanceId TargetTrainUnitInstanceId { get; }
        public bool AttachCarFacingForward { get; }
        public TrainCarAttachTargetEndpoint AttachTargetEndpoint { get; }
        public TrainCarPlacementBlockReason BlockReason { get; }
    }
}
