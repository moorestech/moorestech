using Client.Game.InGame.Train.Unit;
using Game.Train.RailPositions;
using Game.Train.Unit;
using System;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal static class TrainCarPlacementAttachSnapResolver
    {
        // 要件1: N'+M' と既存TrainUnitの重複から、最短の接続先1件を確定する。
        // Requirement 1: Resolve one nearest attach target from overlaps between N'+M' and existing train units.
        internal static IReadOnlyList<TrainInstanceId> ResolveOverlapTrainUnitsForAttachSnap(
            TrainUnitClientCache trainUnitCache,
            RailPosition centerRailPosition,
            RailPositionOverlapDetector.OverlapIndex attachProbeOverlapIndex,
            RailPositionOverlapDetector.OverlapIndex allTrainUnitOverlapIndex,
            int trainLength,
            int attachSnapAdditionalMarginLength,
            out RailPosition attachSnapStartPoint,
            out TrainInstanceId attachTargetTrainInstanceId,
            out bool attachSnapFacingForward,
            out TrainCarAttachTargetEndpoint attachSnapTargetEndpoint)
        {
            attachSnapStartPoint = null;
            attachTargetTrainInstanceId = TrainInstanceId.Empty;
            attachSnapFacingForward = true;
            attachSnapTargetEndpoint = TrainCarAttachTargetEndpoint.Head;

            // 先に many-to-many の重複判定を行い、候補なしなら即終了する。
            // Run many-to-many overlap precheck first and exit early when there is no hit.
            if (trainUnitCache == null ||
                centerRailPosition == null ||
                attachProbeOverlapIndex == null ||
                allTrainUnitOverlapIndex == null ||
                !RailPositionOverlapDetector.HasOverlap(attachProbeOverlapIndex, allTrainUnitOverlapIndex))
            {
                return Array.Empty<TrainInstanceId>();
            }

            // many-to-one再探索で最短候補1件を選定し、向きとendpointを同時に確定する。
            // Rescan as many-to-one and determine nearest target with facing and endpoint.
            var centerForwardPoint = centerRailPosition.GetHeadRailPosition();
            var centerBackwardPoint = centerForwardPoint.DeepCopy();
            centerBackwardPoint.Reverse();
            var nearestTrainInstanceId = TrainInstanceId.Empty;
            var nearestDistance = int.MaxValue;
            var nearestSnapStartPoint = default(RailPosition);
            var nearestAttachFacingForward = true;
            var nearestAttachTargetEndpoint = TrainCarAttachTargetEndpoint.Head;
            var maxSnapDistance = trainLength / 2 + attachSnapAdditionalMarginLength;

            foreach (var pair in trainUnitCache.Units)
            {
                var trainInstanceId = pair.Key;
                var unit = pair.Value;
                if (unit == null || unit.RailPosition == null)
                {
                    continue;
                }
                if (!RailPositionOverlapDetector.HasOverlap(unit.RailPosition, attachProbeOverlapIndex))
                {
                    continue;
                }

                var distance = CalculateNearestSnapDistance(
                    centerForwardPoint,
                    centerBackwardPoint,
                    unit.RailPosition,
                    maxSnapDistance,
                    out var snapStartPoint,
                    out var attachFacingForward,
                    out var attachTargetEndpoint);
                if (distance < 0 || distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestTrainInstanceId = trainInstanceId;
                nearestSnapStartPoint = snapStartPoint;
                nearestAttachFacingForward = attachFacingForward;
                nearestAttachTargetEndpoint = attachTargetEndpoint;
            }

            if (nearestTrainInstanceId == TrainInstanceId.Empty)
            {
                return Array.Empty<TrainInstanceId>();
            }

            attachSnapStartPoint = nearestSnapStartPoint;
            attachTargetTrainInstanceId = nearestTrainInstanceId;
            attachSnapFacingForward = nearestAttachFacingForward;
            attachSnapTargetEndpoint = nearestAttachTargetEndpoint;
            return new[] { nearestTrainInstanceId };

            #region Internal

            int CalculateNearestSnapDistance(
                RailPosition centerForward,
                RailPosition centerBackward,
                RailPosition unitRailPosition,
                int maxCandidateDistance,
                out RailPosition snapStartPoint,
                out bool attachFacingForward,
                out TrainCarAttachTargetEndpoint attachTargetEndpoint)
            {
                snapStartPoint = null;
                attachFacingForward = true;
                attachTargetEndpoint = TrainCarAttachTargetEndpoint.Head;
                if (centerForward == null || centerBackward == null || unitRailPosition == null)
                {
                    return -1;
                }

                // 4パターンの経路距離を比較し、最短候補を採用する。
                // Compare 4 route-distance patterns and keep the minimum candidate.
                var unitHeadReversed = unitRailPosition.GetHeadRailPosition();
                unitHeadReversed.Reverse();
                var unitRearPoint = unitRailPosition.GetRearRailPosition();

                var minDistance = int.MaxValue;
                var minDistanceSnapStartPoint = default(RailPosition);
                var minDistanceAttachFacingForward = true;
                var minDistanceAttachTargetEndpoint = TrainCarAttachTargetEndpoint.Head;
                UpdateMinDistance(centerForward, unitHeadReversed, true, TrainCarAttachTargetEndpoint.Head);
                UpdateMinDistance(centerForward, unitRearPoint, true, TrainCarAttachTargetEndpoint.Rear);
                UpdateMinDistance(centerBackward, unitHeadReversed, false, TrainCarAttachTargetEndpoint.Head);
                UpdateMinDistance(centerBackward, unitRearPoint, false, TrainCarAttachTargetEndpoint.Rear);
                if (minDistance == int.MaxValue)
                {
                    return -1;
                }

                snapStartPoint = minDistanceSnapStartPoint;
                attachFacingForward = minDistanceAttachFacingForward;
                attachTargetEndpoint = minDistanceAttachTargetEndpoint;
                return minDistance;

                #region Internal

                void UpdateMinDistance(
                    RailPosition from,
                    RailPosition to,
                    bool isCenterForwardSide,
                    TrainCarAttachTargetEndpoint candidateAttachTargetEndpoint)
                {
                    var distance = RailPositionRouteDistanceFinder.FindShortestDistance(from, to);
                    if (distance < 0 || distance > maxCandidateDistance)
                    {
                        return;
                    }
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        minDistanceSnapStartPoint = to.DeepCopy();
                        minDistanceAttachFacingForward = isCenterForwardSide;
                        minDistanceAttachTargetEndpoint = candidateAttachTargetEndpoint;
                    }
                }

                #endregion
            }

            #endregion
        }
    }
}
