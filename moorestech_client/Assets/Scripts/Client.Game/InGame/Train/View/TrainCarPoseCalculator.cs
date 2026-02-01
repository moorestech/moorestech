using System;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using UnityEngine;

namespace Client.Game.InGame.Train.View
{
    public static class TrainCarPoseCalculator
    {
        private const int ArcLengthSamples = 64;
        private const float MinCurveLength = 1e-4f;

        // 列車先頭から指定距離の位置と向きを算出する
        // Calculate position and forward direction at the specified head distance
        public static bool TryGetPose(RailPosition railPosition, int distanceFromHead, out Vector3 position, out Vector3 forward)
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

        private static bool TryGetPoseOnSegment(IRailNode behind, IRailNode ahead, int distanceFromBehind, out Vector3 position, out Vector3 forward)
        {
            // 出力を初期化する
            // Initialize output values
            position = default;
            forward = Vector3.forward;

            // セグメント入力を検証する
            // Validate segment inputs
            if (behind == null || ahead == null || distanceFromBehind < 0) return false;

            // ベジエ制御点を相対座標で構成する
            // Build control points from segment strength
            BezierUtility.Getp0p1p2p3(behind, ahead, out var p0, out var p1, out var p2, out var p3);
            // 弧長テーブルを用いてtを解決する
            // Resolve t with arc-length lookup
            var arcLength = BuildArcLengthTable(p0, p1, p2, p3, out var arcLengths);
            var distanceWorld = distanceFromBehind / BezierUtility.RAIL_LENGTH_SCALE;
            var delta = p3 - p0;
            var t = arcLength > MinCurveLength ? BezierUtility.DistanceToTime(distanceWorld, arcLength, arcLengths) : ComputeLinearT(distanceWorld, delta);
            // 位置と向きを計算する
            // Compute position and forward vector
            position = BezierUtility.GetBezierPoint(p0, p1, p2, p3, t);
            var tangent = BezierUtility.GetBezierTangent(p0, p1, p2, p3, t);
            forward = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : (delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.forward);
            return true;
        }

        private static float BuildArcLengthTable(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, out float[] arcLengths)
        {
            // 弧長テーブルは毎回生成する
            // Build arc-length table every time
            arcLengths = Array.Empty<float>();
            return BezierUtility.BuildArcLengthTable(p0, p1, p2, p3, ArcLengthSamples, ref arcLengths);
        }

        

        private static float ComputeLinearT(float distanceWorld, Vector3 delta)
        {
            // 直線補間用のtを計算する
            // Compute linear interpolation t
            var straightLength = Mathf.Max(MinCurveLength, delta.magnitude);
            return Mathf.Clamp01(distanceWorld / straightLength);
        }


        #endregion
    }
}
