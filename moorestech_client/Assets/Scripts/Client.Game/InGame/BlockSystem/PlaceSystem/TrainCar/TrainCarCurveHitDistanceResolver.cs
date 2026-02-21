using Game.Train.RailCalc;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal sealed class TrainCarCurveHitDistanceResolver
    {
        private const int CurveSampleCount = 128;
        private const int CurveRefineIterations = 6;
        private const float MinCurveLength = 1e-4f;
        private float[] _arcLengthBuffer;

        // レール描画カーブ上でヒット点の始点距離を求める
        // Resolve the start distance of a hit point on the render curve
        public bool TryFindDistanceFromStartOnCurve(IRailNode startNode, IRailNode endNode, Vector3 hitPosition, out float distanceFromStart)
        {
            // 出力値を初期化し入力を検証する
            // Initialize output and validate inputs
            distanceFromStart = 0f;
            if (startNode == null || endNode == null)
            {
                return false;
            }

            // 描画制御点を組み立て、サンプル点の弧長を蓄積する
            // Build render control points and accumulate sample arc lengths
            BezierUtility.BuildRenderControlPoints(startNode.FrontControlPoint, endNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            var steps = CurveSampleCount;
            var arcLengths = PrepareArcLengthBuffer(steps + 1);
            arcLengths[0] = 0f;
            var previous = BezierUtility.GetBezierPoint(p0, p1, p2, p3, 0f);
            var bestIndex = 0;
            var bestDistanceSq = (previous - hitPosition).sqrMagnitude;

            for (var i = 1; i <= steps; i++)
            {
                // 最近点候補インデックスを更新する
                // Update nearest-point candidate index
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

            // 曲線長が極小なら無効として扱う
            // Treat near-zero curve length as invalid
            var curveLength = arcLengths[steps];
            if (curveLength <= MinCurveLength)
            {
                return false;
            }

            // 近傍区間を三分探索で絞り、弧長を線形補間する
            // Narrow local interval by ternary search, then interpolate arc length
            var refinedT = RefineClosestTime(p0, p1, p2, p3, hitPosition, steps, bestIndex);
            distanceFromStart = EvaluateArcLength(arcLengths, refinedT, steps);
            return true;

            #region Internal

            float RefineClosestTime(Vector3 start, Vector3 control0, Vector3 control1, Vector3 end, Vector3 target, int sampleSteps, int baseIndex)
            {
                // 粗探索の近傍のみを対象にして反復回数を固定する
                // Restrict to coarse-nearest neighborhood with fixed iterations
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
                // tを区間インデックスへ変換して弧長を補間する
                // Convert t to interval indices and interpolate arc length
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

        #region Internal

        private float[] PrepareArcLengthBuffer(int length)
        {
            // 配列を使い回してGC発生を抑える
            // Reuse array buffer to suppress GC allocations
            if (_arcLengthBuffer == null || _arcLengthBuffer.Length != length)
            {
                _arcLengthBuffer = new float[length];
            }
            return _arcLengthBuffer;
        }

        #endregion
    }
}
