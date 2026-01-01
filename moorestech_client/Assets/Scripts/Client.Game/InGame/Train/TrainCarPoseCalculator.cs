using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Utility;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    public sealed class TrainCarPoseCalculator
    {
        private const int ArcLengthSamples = 64;
        private const float MinCurveLength = 1e-4f;
        private readonly Dictionary<ulong, SegmentArcLengthCache> _segmentCaches = new();

        // 列車先頭から指定距離の位置と向きを算出する
        // Calculate position and forward direction at the specified head distance
        public bool TryGetPose(RailPosition railPosition, int distanceFromHead, out Vector3 position, out Vector3 forward)
        {
            // 出力を初期化する
            // Initialize output values
            position = default;
            forward = Vector3.forward;

            // 入力とノード列を検証する
            // Validate inputs and node list
            if (railPosition == null || distanceFromHead < 0) return false;
            var railNodes = railPosition.GetRailNodes();
            if (railNodes == null || railNodes.Count < 2) return false;

            // 先頭セグメントを基準に距離を評価する
            // Evaluate distance starting from the head segment
            var distanceToNext = railPosition.GetDistanceToNextNode();
            var headAhead = railNodes[0];
            var headBehind = railNodes[1];
            var headLength = headBehind.GetDistanceToNode(headAhead);
            if (headLength <= 0) return false;

            var distanceFromBehindToHead = headLength - distanceToNext;
            if (distanceFromBehindToHead < 0) return false;
            if (distanceFromHead <= distanceFromBehindToHead)
            {
                var distanceFromBehind = distanceFromBehindToHead - distanceFromHead;
                return TryGetPoseOnSegment(headBehind, headAhead, distanceFromBehind, out position, out forward);
            }

            // 残距離で後方セグメントを辿る
            // Walk rear segments by consuming the remaining distance
            var remaining = distanceFromHead - distanceFromBehindToHead;
            for (var i = 1; i < railNodes.Count - 1; i++)
            {
                var ahead = railNodes[i];
                var behind = railNodes[i + 1];
                var segmentLength = behind.GetDistanceToNode(ahead);
                if (segmentLength <= 0) return false;
                if (remaining <= segmentLength)
                {
                    var distanceFromBehind = segmentLength - remaining;
                    return TryGetPoseOnSegment(behind, ahead, distanceFromBehind, out position, out forward);
                }
                remaining -= segmentLength;
            }

            return false;
        }

        #region Internal

        private bool TryGetPoseOnSegment(IRailNode behind, IRailNode ahead, int distanceFromBehind, out Vector3 position, out Vector3 forward)
        {
            // 出力を初期化する
            // Initialize output values
            position = default;
            forward = Vector3.forward;

            // セグメント入力を検証する
            // Validate segment inputs
            if (behind == null || ahead == null || distanceFromBehind < 0) return false;

            // ベジエ制御点を相対座標で構成する
            // Build relative Bezier control points
            var startControl = behind.FrontControlPoint;
            var endControl = ahead.BackControlPoint;
            var origin = startControl.OriginalPosition;
            var p0 = Vector3.zero;
            var p1 = startControl.ControlPointPosition;
            var delta = endControl.OriginalPosition - origin;
            var p2 = endControl.ControlPointPosition + delta;
            var p3 = delta;

            // 弧長テーブルを用いてtを解決する
            // Resolve t with arc-length lookup
            var arcLength = EnsureArcLengthCache(behind, ahead, p0, p1, p2, p3, out var arcLengths);
            var distanceWorld = distanceFromBehind / BezierUtility.RAIL_LENGTH_SCALE;
            var t = arcLength > MinCurveLength ? BezierUtility.DistanceToTime(distanceWorld, arcLength, arcLengths) : ComputeLinearT(distanceWorld, p3);

            // 位置と向きを計算する
            // Compute position and forward vector
            position = origin + BezierUtility.GetBezierPoint(p0, p1, p2, p3, t);
            var tangent = BezierUtility.GetBezierTangent(p0, p1, p2, p3, t);
            forward = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : (delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.forward);
            return true;
        }

        private float EnsureArcLengthCache(IRailNode behind, IRailNode ahead, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, out float[] arcLengths)
        {
            // キャチE��ュを参�Eして弧長テーブルを再利用する
            // Reuse cached arc-length table when available
            var key = ComputeSegmentKey(behind.NodeId, ahead.NodeId);
            if (!_segmentCaches.TryGetValue(key, out var cache) || cache.StartGuid != behind.NodeGuid || cache.EndGuid != ahead.NodeGuid)
            {
                var reuse = cache?.ArcLengths;
                var curveLength = BezierUtility.BuildArcLengthTable(p0, p1, p2, p3, ArcLengthSamples, ref reuse);
                cache = new SegmentArcLengthCache(behind.NodeGuid, ahead.NodeGuid, curveLength, reuse);
                _segmentCaches[key] = cache;
            }

            arcLengths = cache.ArcLengths;
            return cache.CurveLength;
        }

        private static float ComputeLinearT(float distanceWorld, Vector3 delta)
        {
            // 直線補間用のtを計算する
            // Compute linear interpolation t
            var straightLength = Mathf.Max(MinCurveLength, delta.magnitude);
            return Mathf.Clamp01(distanceWorld / straightLength);
        }

        private static ulong ComputeSegmentKey(int behindNodeId, int aheadNodeId)
        {
            return (uint)behindNodeId + ((ulong)(uint)aheadNodeId << 32);
        }

        private sealed class SegmentArcLengthCache
        {
            public SegmentArcLengthCache(Guid startGuid, Guid endGuid, float curveLength, float[] arcLengths)
            {
                StartGuid = startGuid;
                EndGuid = endGuid;
                CurveLength = curveLength;
                ArcLengths = arcLengths ?? Array.Empty<float>();
            }

            public Guid StartGuid { get; }
            public Guid EndGuid { get; }
            public float CurveLength { get; }
            public float[] ArcLengths { get; }
        }

        #endregion
    }
}
