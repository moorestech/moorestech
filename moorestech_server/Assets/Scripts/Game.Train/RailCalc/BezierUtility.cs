using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.RailCalc
{
    public static class BezierUtility
    {
        public const float RAIL_LENGTH_SCALE = 1024.0f;

        // ベジエ曲線上の座標を計算
        // Evaluate point on cubic Bezier curve
        public static Vector3 GetBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            if (IsZeroLength(p0, p3) || IsStraightLine(p0, p1, p2, p3))
                return Vector3.Lerp(p0, p3, t);

            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 point = uuu * p0;
            point += 3f * uu * t * p1;
            point += 3f * u * tt * p2;
            point += ttt * p3;
            return point;
        }

        // ベジエ曲線の接線ベクトルを計算
        // Evaluate normalized tangent on cubic Bezier curve
        public static Vector3 GetBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            if (IsZeroLength(p0, p3))
                return Vector3.forward;

            Vector3 straightDir = p3 - p0;
            if (IsStraightLine(p0, p1, p2, p3) && straightDir.sqrMagnitude > 1e-6f)
                return straightDir.normalized;

            float u = 1f - t;
            Vector3 term0 = (p1 - p0) * (3f * u * u);
            Vector3 term1 = (p2 - p1) * (6f * u * t);
            Vector3 term2 = (p3 - p2) * (3f * t * t);
            Vector3 derivative = term0 + term1 + term2;
            return derivative.sqrMagnitude > 1e-6f ? derivative.normalized : (straightDir.sqrMagnitude > 1e-6f ? straightDir.normalized : Vector3.forward);
        }

        // RailControlPointを利用した座標取得
        // Evaluate point using RailControlPoints
        public static Vector3 GetBezierPoint(RailControlPoint startControlPoint, RailControlPoint endControlPoint, float t)
        {
            BuildRelativeControlPoints(startControlPoint, endControlPoint, out var origin, out var p0, out var p1, out var p2, out var p3);
            Vector3 relative = GetBezierPoint(p0, p1, p2, p3, t);
            return relative + origin;
        }

        // RailControlPointを利用した接線取得
        // Evaluate tangent using RailControlPoints
        public static Vector3 GetBezierTangent(RailControlPoint startControlPoint, RailControlPoint endControlPoint, float t)
        {
            BuildRelativeControlPoints(startControlPoint, endControlPoint, out _, out var p0, out var p1, out var p2, out var p3);
            return GetBezierTangent(p0, p1, p2, p3, t);
        }

        // 3次ベジエ曲線の概算距離
        // Approximate curve length by sampling
        public static float GetBezierCurveLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples)
        {
            if (IsZeroLength(p0, p3))
                return 0f;
            if (IsStraightLine(p0, p1, p2, p3))
                return Vector3.Distance(p0, p3);

            int steps = Mathf.Max(8, samples);
            float length = 0f;
            Vector3 previousPoint = GetBezierPoint(p0, p1, p2, p3, 0f);

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 currentPoint = GetBezierPoint(p0, p1, p2, p3, t);
                length += Vector3.Distance(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
            return length;
        }

        // RailControlPointの概算距離
        // Approximate curve length using RailControlPoints
        public static float GetBezierCurveLength(RailControlPoint cp0, RailControlPoint cp1, int samples = 512)
        {
            EnsureDeterministicOrder(ref cp0, ref cp1);
            BuildRelativeControlPoints(cp0, cp1, out _, out var p0, out var p1, out var p2, out var p3);
            return GetBezierCurveLength(p0, p1, p2, p3, samples);
        }

        // RailControlPointを相対座標に変換
        // Convert RailControlPoints to relative control points
        public static void BuildRelativeControlPoints(RailControlPoint startControlPoint, RailControlPoint endControlPoint, out Vector3 origin, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            origin = startControlPoint.OriginalPosition;
            p0 = Vector3.zero;
            p1 = startControlPoint.ControlPointPosition;
            Vector3 delta = endControlPoint.OriginalPosition - origin;
            p2 = endControlPoint.ControlPointPosition + delta;
            p3 = delta;
        }

        // 制御点の順番を安定化
        // Ensure deterministic ordering of control points
        private static void EnsureDeterministicOrder(ref RailControlPoint cp0, ref RailControlPoint cp1)
        {
            if (!IsLexicographicallySmaller(cp1.OriginalPosition, cp0.OriginalPosition))
                return;

            RailControlPoint tmp = cp0;
            cp0 = cp1;
            cp1 = tmp;
        }

        // 位置比較を辞書式で実施
        // Lexicographical comparison helper
        private static bool IsLexicographicallySmaller(Vector3 a, Vector3 b)
        {
            if (a.x < b.x) return true;
            if (a.x > b.x) return false;
            if (a.y < b.y) return true;
            if (a.y > b.y) return false;
            return a.z < b.z;
        }

        // 弧長テーブルを構築
        // Build arc-length lookup table
        public static float BuildArcLengthTable(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples, ref float[] arcLengths)
        {
            int steps = Mathf.Max(8, samples);
            if (arcLengths == null || arcLengths.Length != steps + 1)
                arcLengths = new float[steps + 1];

            arcLengths[0] = 0f;

            if (IsZeroLength(p0, p3))
            {
                for (int i = 1; i <= steps; i++)
                    arcLengths[i] = 0f;
                return 0f;
            }

            if (IsStraightLine(p0, p1, p2, p3))
            {
                float totalLine = Vector3.Distance(p0, p3);
                for (int i = 1; i <= steps; i++)
                    arcLengths[i] = totalLine * i / steps;
                return totalLine;
            }

            Vector3 previous = GetBezierPoint(p0, p1, p2, p3, 0f);
            float total = 0f;

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 current = GetBezierPoint(p0, p1, p2, p3, t);
                total += Vector3.Distance(previous, current);
                arcLengths[i] = total;
                previous = current;
            }

            return total;
        }

        // 距離からtを求める
        // Convert travelled distance to Bezier parameter t
        public static float DistanceToTime(float distance, float curveLength, float[] arcLengths)
        {
            if (arcLengths == null || arcLengths.Length < 2 || curveLength <= 1e-5f)
                return 0f;

            distance = Mathf.Clamp(distance, 0f, curveLength);
            int steps = arcLengths.Length - 1;

            for (int i = 1; i <= steps; i++)
            {
                float prev = arcLengths[i - 1];
                float current = arcLengths[i];
                if (distance > current)
                    continue;

                float lerp = Mathf.Approximately(current, prev) ? 0f : (distance - prev) / (current - prev);
                float stepSize = 1f / steps;
                return Mathf.Lerp((i - 1) * stepSize, i * stepSize, lerp);
            }

            return 1f;
        }

        private static bool IsZeroLength(Vector3 p0, Vector3 p3) => Vector3.Distance(p0, p3) <= 1e-6f;

        private static bool IsStraightLine(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Vector3 direction = p3 - p0;
            if (direction.sqrMagnitude <= 1e-6f)
                return true;

            Vector3 n = direction.normalized;
            return IsAligned(p1 - p0, n) && IsAligned(p2 - p0, n);
        }

        private static bool IsAligned(Vector3 vector, Vector3 normal)
        {
            if (vector.sqrMagnitude <= 1e-6f)
                return true;
            Vector3 cross = Vector3.Cross(vector, normal);
            return cross.sqrMagnitude <= 1e-6f;
        }
    }
}
