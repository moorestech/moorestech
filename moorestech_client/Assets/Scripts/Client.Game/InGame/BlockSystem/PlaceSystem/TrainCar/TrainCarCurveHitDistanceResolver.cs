using Game.Train.RailCalc;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal static class TrainCarCurveHitDistanceResolver
    {
        private const int CurveSampleCount = 128;
        private const int CurveRefineIterations = 6;
        private const float MinCurveLength = 1e-4f;

        // 日本語: カーブ上ヒット点の始点距離を求める
        // English: Resolve distance from curve start to the hit point.
        internal static bool TryFindDistanceFromStartOnCurve(IRailNode startNode, IRailNode endNode, Vector3 hitPosition, out float distanceFromStart)
        {
            distanceFromStart = 0f;
            if (startNode == null || endNode == null)
            {
                return false;
            }

            BezierUtility.BuildRenderControlPoints(startNode.FrontControlPoint, endNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            var steps = CurveSampleCount;
            var arcLengths = new float[steps + 1];
            arcLengths[0] = 0f;
            var previous = BezierUtility.GetBezierPoint(p0, p1, p2, p3, 0f);
            var bestIndex = 0;
            var bestDistanceSq = (previous - hitPosition).sqrMagnitude;

            for (var i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var point = BezierUtility.GetBezierPoint(p0, p1, p2, p3, t);
                arcLengths[i] = arcLengths[i - 1] + Vector3.Distance(previous, point);
                var distanceSq = (point - hitPosition).sqrMagnitude;
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestIndex = i;
                }
                previous = point;
            }

            var curveLength = arcLengths[steps];
            if (curveLength <= MinCurveLength)
            {
                return false;
            }

            var refinedT = RefineClosestTime(p0, p1, p2, p3, hitPosition, steps, bestIndex);
            distanceFromStart = EvaluateArcLength(arcLengths, refinedT, steps);
            return true;

            #region Internal

            float RefineClosestTime(Vector3 start, Vector3 control0, Vector3 control1, Vector3 end, Vector3 target, int sampleSteps, int baseIndex)
            {
                var lowIndex = Mathf.Max(0, baseIndex - 1);
                var highIndex = Mathf.Min(sampleSteps, baseIndex + 1);
                var low = (float)lowIndex / sampleSteps;
                var high = (float)highIndex / sampleSteps;

                for (var i = 0; i < CurveRefineIterations; i++)
                {
                    var range = high - low;
                    var t1 = low + range / 3f;
                    var t2 = high - range / 3f;
                    var d1 = (BezierUtility.GetBezierPoint(start, control0, control1, end, t1) - target).sqrMagnitude;
                    var d2 = (BezierUtility.GetBezierPoint(start, control0, control1, end, t2) - target).sqrMagnitude;
                    if (d1 <= d2)
                    {
                        high = t2;
                        continue;
                    }
                    low = t1;
                }

                return (low + high) * 0.5f;
            }

            float EvaluateArcLength(float[] lengthTable, float t, int sampleSteps)
            {
                var clamped = Mathf.Clamp01(t);
                var scaled = clamped * sampleSteps;
                var index = Mathf.FloorToInt(scaled);
                if (index >= sampleSteps)
                {
                    return lengthTable[sampleSteps];
                }
                var nextIndex = index + 1;
                var ratio = scaled - index;
                return Mathf.Lerp(lengthTable[index], lengthTable[nextIndex], ratio);
            }

            #endregion
        }
    }
}
