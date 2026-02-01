using UnityEngine;

namespace Game.Train.RailCalc
{
    public static class RailSegmentCurveUtility
    {
        // セグメント強度を直線距離から算出する
        // Calculate segment strength from straight distance
        public static float CalculateSegmentStrength(Vector3 startPosition, Vector3 endPosition)
        {
            return Vector3.Distance(startPosition, endPosition);
        }

        // セグメント用の制御点を構築する
        // Build control points for a segment
        public static void BuildControlPoints(Vector3 startPosition, Vector3 startDirection, Vector3 endPosition, Vector3 endDirection, float strength, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            var startDir = NormalizeOrFallback(startDirection, endPosition - startPosition, Vector3.forward);
            var endDir = NormalizeOrFallback(endDirection, startPosition - endPosition, Vector3.back);
            p0 = startPosition;
            p1 = startPosition + startDir * strength;
            p2 = endPosition + endDir * strength;
            p3 = endPosition;
        }

        // セグメントの曲線長を取得する
        // Calculate bezier length for a segment
        public static float GetBezierCurveLength(Vector3 startPosition, Vector3 startDirection, Vector3 endPosition, Vector3 endDirection, float strength, int samples)
        {
            BuildControlPoints(startPosition, startDirection, endPosition, endDirection, strength, out var p0, out var p1, out var p2, out var p3);
            return BezierUtility.GetBezierCurveLength(p0, p1, p2, p3, samples);
        }

        #region Internal

        // 方向ベクトルの正規化とフォールバックをまとめる
        // Normalize direction vectors with fallbacks
        private static Vector3 NormalizeOrFallback(Vector3 direction, Vector3 fallback, Vector3 defaultFallback)
        {
            if (direction.sqrMagnitude > 1e-6f) return direction.normalized;
            if (fallback.sqrMagnitude > 1e-6f) return fallback.normalized;
            return defaultFallback;
        }

        #endregion
    }
}
