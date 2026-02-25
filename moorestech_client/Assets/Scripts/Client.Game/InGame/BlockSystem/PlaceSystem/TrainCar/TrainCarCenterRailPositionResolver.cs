using Client.Game.InGame.Train.RailGraph;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal static class TrainCarCenterRailPositionResolver
    {
        // 日本語: レイヒット位置から中心RailPositionを作る
        // English: Resolve center RailPosition from raycast hit on rail segment.
        internal static bool TryResolveCenterRailPosition(
            Vector3 hitPosition,
            RailObjectIdCarrier carrier,
            RailGraphClientCache cache,
            out RailPosition centerRailPosition)
        {
            centerRailPosition = null;

            if (cache == null || carrier == null || !TryResolveCanonicalNodes(cache, carrier.GetRailObjectId(), out var canonicalFromNode, out var canonicalToNode))
            {
                return false;
            }
            if (!TrainCarCurveHitDistanceResolver.TryFindDistanceFromStartOnCurve(canonicalFromNode, canonicalToNode, hitPosition, out var distanceFromStartWorld))
            {
                return false;
            }

            var segmentDistance = canonicalFromNode.GetDistanceToNode(canonicalToNode);
            if (segmentDistance <= 0)
            {
                return false;
            }
            var distanceFromStartRail = Mathf.RoundToInt(distanceFromStartWorld * BezierUtility.RAIL_LENGTH_SCALE);
            distanceFromStartRail = Mathf.Clamp(distanceFromStartRail, 0, segmentDistance);
            var distanceToNext = segmentDistance - distanceFromStartRail;
            centerRailPosition = new RailPosition(new List<IRailNode> { canonicalToNode, canonicalFromNode }, 0, distanceToNext);
            return true;
        }

        #region Internal

        private static bool TryResolveCanonicalNodes(
            RailGraphClientCache cache,
            ulong railObjectId,
            out IRailNode canonicalFromNode,
            out IRailNode canonicalToNode)
        {
            var canonicalFromId = (int)(railObjectId & 0xffffffff);
            var canonicalToId = (int)(railObjectId >> 32);
            canonicalFromNode = null;
            canonicalToNode = null;
            if (!cache.TryGetNode(canonicalFromId, out canonicalFromNode))
            {
                return false;
            }
            if (!cache.TryGetNode(canonicalToId, out canonicalToNode))
            {
                return false;
            }
            return true;
        }

        #endregion
    }
}
