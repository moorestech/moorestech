using Game.Train.Utility;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    internal static class BezierRailCurveCalculator
    {
        private const float MinimumTangentLength = 0.05f;
        private const float ShortDistanceThreshold = 0.5f;

        public static bool ValidateRailConnection(Vector3? fromOrigin, Vector3? fromControlOffset, Vector3? toOrigin, Vector3? toControlOffset)
        {
            return fromOrigin.HasValue && fromControlOffset.HasValue && toOrigin.HasValue && toControlOffset.HasValue;
        }

        public static (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) CalculateBezierControlPoints(Vector3 fromOrigin, Vector3 fromControlOffset, Vector3 toOrigin, Vector3 toControlOffset, int distance)
        {
            var startPosition = EnsureFinite(fromOrigin);
            var startOffset = EnsureFinite(fromControlOffset);
            var endPosition = EnsureFinite(toOrigin);
            var endOffset = EnsureFinite(toControlOffset);

            var p0 = startPosition;
            var p1 = startPosition + startOffset;
            var p3 = endPosition;
            var p2 = endPosition + endOffset;

            var worldDistance = Mathf.Abs(distance) / BezierUtility.RAIL_LENGTH_SCALE;
            var clampLength = Mathf.Max(worldDistance * 0.5f, MinimumTangentLength);
            if (worldDistance < ShortDistanceThreshold)
            {
                clampLength = Mathf.Max(worldDistance * 0.3f, MinimumTangentLength * 0.5f);
            }

            var tangentOut = ClampVector(p1 - p0, clampLength);
            var tangentIn = ClampVector(p2 - p3, clampLength);

            p1 = p0 + tangentOut;
            p2 = p3 + tangentIn;

            return (p0, p1, p2, p3);
        }

        private static Vector3 EnsureFinite(Vector3 value)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)) return Vector3.zero;
            if (float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z)) return Vector3.zero;
            return value;
        }

        private static Vector3 ClampVector(Vector3 value, float maxLength)
        {
            if (value == Vector3.zero) return value;
            var sqrLimit = maxLength * maxLength;
            return value.sqrMagnitude <= sqrLimit ? value : value.normalized * maxLength;
        }
    }
}
