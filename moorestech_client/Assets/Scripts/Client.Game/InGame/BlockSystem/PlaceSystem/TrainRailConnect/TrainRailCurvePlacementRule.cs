using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public static class TrainRailCurvePlacementRule
    {
        public const float MinimumPlaceableCurveRadius = 8f;
        private const int CurveRadiusSampleCount = 32;
        private const float DerivativeMagnitudeTolerance = 1e-6f;
        private const float CurvatureTolerance = 1e-6f;

        public static bool IsPlaceable(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var minimumRadius = CalculateMinimumCurveRadius(p0, p1, p2, p3);
            return minimumRadius >= MinimumPlaceableCurveRadius;
        }

        public static float CalculateMinimumCurveRadius(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var minimumRadius = float.PositiveInfinity;

            // Bezier全体を等間隔にサンプルして最小Rを拾う。
            // Sample the whole Bezier curve and keep the tightest radius.
            for (var i = 0; i <= CurveRadiusSampleCount; i++)
            {
                var t = (float)i / CurveRadiusSampleCount;
                var radius = CalculateRadiusAt(t);
                minimumRadius = Mathf.Min(minimumRadius, radius);
            }

            return minimumRadius;

            #region Internal

            float CalculateRadiusAt(float t)
            {
                var firstDerivative = CalculateFirstDerivative(t);
                if (firstDerivative.sqrMagnitude <= DerivativeMagnitudeTolerance)
                {
                    return 0f;
                }

                // 曲率がほぼ0なら直線扱いとしてR無限大にする。
                // Treat near-zero curvature as a straight segment with infinite radius.
                var secondDerivative = CalculateSecondDerivative(t);
                var cross = Vector3.Cross(firstDerivative, secondDerivative);
                if (cross.sqrMagnitude <= CurvatureTolerance)
                {
                    return float.PositiveInfinity;
                }

                var speed = firstDerivative.magnitude;
                return speed * speed * speed / cross.magnitude;
            }

            Vector3 CalculateFirstDerivative(float t)
            {
                var u = 1f - t;
                return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
            }

            Vector3 CalculateSecondDerivative(float t)
            {
                var u = 1f - t;
                return 6f * u * (p2 - 2f * p1 + p0) + 6f * t * (p3 - 2f * p2 + p1);
            }

            #endregion
        }
    }
}
