using Client.Game.InGame.Train.RailGraph;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal sealed class TrainCarCenterRailPositionResolver
    {
        private readonly RailGraphClientCache _cache;
        private readonly TrainCarCurveHitDistanceResolver _curveHitDistanceResolver;

        internal TrainCarCenterRailPositionResolver(RailGraphClientCache cache, TrainCarCurveHitDistanceResolver curveHitDistanceResolver)
        {
            _cache = cache;
            _curveHitDistanceResolver = curveHitDistanceResolver;
        }

        internal bool TryResolveCenterRailPosition(Vector3 hitPosition, RailObjectIdCarrier carrier, out RailPosition centerRailPosition)
        {
            centerRailPosition = null;

            // 入力から対象セグメントの正規ノードを解決する
            // Resolve canonical segment nodes from the hit input
            if (carrier == null || !TryResolveCanonicalNodes(carrier.GetRailObjectId(), out var canonicalFromNode, out var canonicalToNode))
            {
                return false;
            }

            // カーブ最近点から始点基準の弧長距離を求める
            // Calculate arc-length distance from segment start via nearest point on curve
            if (!_curveHitDistanceResolver.TryFindDistanceFromStartOnCurve(canonicalFromNode, canonicalToNode, hitPosition, out var distanceFromStartWorld))
            {
                return false;
            }

            // セグメント距離をrail単位へ変換して中心RailPositionを組み立てる
            // Convert into rail units and compose center RailPosition
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

        private bool TryResolveCanonicalNodes(ulong railObjectId, out IRailNode canonicalFromNode, out IRailNode canonicalToNode)
        {
            var canonicalFromId = (int)(railObjectId & 0xffffffff);
            var canonicalToId = (int)(railObjectId >> 32);
            canonicalFromNode = null;
            canonicalToNode = null;
            if (!_cache.TryGetNode(canonicalFromId, out canonicalFromNode))
            {
                return false;
            }
            if (!_cache.TryGetNode(canonicalToId, out canonicalToNode))
            {
                return false;
            }
            return true;
        }

        #endregion
    }
}
