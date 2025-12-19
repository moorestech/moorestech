using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.Utility
{
    public static class BezierUtility
    {
        /// <summary>
        /// t を与えたときにベジェ曲線上の座標を返す関数（3次ベジェ曲線）
        /// Returns the position on a cubic Bezier curve for the given parameter t
        /// </summary>
        public const float RAIL_LENGTH_SCALE = 1024.0f;

        public static Vector3 GetBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            if (p0 == p3)
                return p0;

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

        /// <summary>
        /// 3次ベジェ曲線の概算距離を返す関数
        /// Approximates the length of a cubic Bezier curve with the given sampling count
        /// </summary>
        public static float GetBezierCurveLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples)
        {
            float length = 0f;
            Vector3 previousPoint = GetBezierPoint(p0, p1, p2, p3, 0f);

            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector3 currentPoint = GetBezierPoint(p0, p1, p2, p3, t);
                length += Vector3.Distance(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
            return length;
        }

        /// <summary>
        /// RailControlPointの座標を受け取り概算距離を返す、OriginalPositionが大きい場合の誤差に対応
        /// Estimates curve length directly from RailControlPoints and stabilizes large coordinate inputs
        /// </summary>
        public static float GetBezierCurveLength(RailControlPoint cp0, RailControlPoint cp1, int samples = 512)
        {
            EnsureDeterministicOrder(ref cp0, ref cp1);
            BuildRelativeControlPoints(cp0, cp1, out _, out var p0, out var p1, out var p2, out var p3);
            return GetBezierCurveLength(p0, p1, p2, p3, samples);
        }

        /// <summary>
        /// RailControlPointを直接渡してベジェ曲線上のワールド座標を取得する
        /// Returns the world position on the Bezier curve defined by two RailControlPoints
        /// </summary>
        public static Vector3 GetBezierPoint(RailControlPoint startControlPoint, RailControlPoint endControlPoint, float t)
        {
            BuildRelativeControlPoints(startControlPoint, endControlPoint, out var origin, out var p0, out var p1, out var p2, out var p3);
            Vector3 relative = GetBezierPoint(p0, p1, p2, p3, Mathf.Clamp01(t));
            return relative + origin;
        }

        private static void BuildRelativeControlPoints(RailControlPoint startControlPoint, RailControlPoint endControlPoint, out Vector3 origin, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            origin = startControlPoint.OriginalPosition;
            p0 = Vector3.zero;
            p1 = startControlPoint.ControlPointPosition;
            Vector3 delta = endControlPoint.OriginalPosition - origin;
            p2 = endControlPoint.ControlPointPosition + delta;
            p3 = delta;
        }

        private static void EnsureDeterministicOrder(ref RailControlPoint cp0, ref RailControlPoint cp1)
        {
            if (!IsLexicographicallySmaller(cp1.OriginalPosition, cp0.OriginalPosition))
                return;

            var tmp = cp0;
            cp0 = cp1;
            cp1 = tmp;
        }

        private static bool IsLexicographicallySmaller(Vector3 a, Vector3 b)
        {
            if (a.x < b.x) return true;
            if (a.x > b.x) return false;
            if (a.y < b.y) return true;
            if (a.y > b.y) return false;
            return a.z < b.z;
        }
    }
}
