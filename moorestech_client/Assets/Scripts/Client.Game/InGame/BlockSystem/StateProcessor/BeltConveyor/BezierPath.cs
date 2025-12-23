using System;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.BeltConveyor
{
    /// <summary>
    /// 3次ベジェ曲線の計算ユーティリティ
    /// Utility for cubic Bezier curve calculations
    /// </summary>
    [Serializable]
    public class BezierPath
    {
        public Vector3 StartPoint => startPoint;
        [SerializeField] private Vector3 startPoint;

        public Vector3 StartControlPoint => startControlPoint;
        [SerializeField] private Vector3 startControlPoint;

        public Vector3 EndControlPoint => endControlPoint;
        [SerializeField] private Vector3 endControlPoint;

        public Vector3 EndPoint => endPoint;
        [SerializeField] private Vector3 endPoint;

        // 制御点のワールド座標を取得
        // Get world position of control points
        public Vector3 StartControlWorldPosition => startPoint + startControlPoint;
        public Vector3 EndControlWorldPosition => endPoint + endControlPoint;

        /// <summary>
        /// t（0.0-1.0）に対応するベジェ曲線上の点を返す
        /// Returns point on Bezier curve for given t (0.0-1.0)
        /// </summary>
        public Vector3 GetPoint(float t)
        {
            t = Mathf.Clamp01(t);

            // 始点と終点が同じ場合は始点を返す
            // Return start point if start and end are the same
            if (startPoint == endPoint) return startPoint;

            return CalculateBezierPoint();

            #region Internal

            Vector3 CalculateBezierPoint()
            {
                float u = 1f - t;
                float tt = t * t;
                float uu = u * u;
                float uuu = uu * u;
                float ttt = tt * t;

                // 制御点のワールド座標を計算
                // Calculate world coordinates of control points
                Vector3 p1World = startPoint + startControlPoint;
                Vector3 p2World = endPoint + endControlPoint;

                // 3次ベジェ曲線の計算
                // Cubic Bezier curve calculation
                Vector3 point = uuu * startPoint;           // (1 - t)^3 * p0
                point += 3f * uu * t * p1World;             // 3(1 - t)^2 * t * p1
                point += 3f * u * tt * p2World;             // 3(1 - t) * t^2 * p2
                point += ttt * endPoint;                    // t^3 * p3

                return point;
            }

            #endregion
        }

#if UNITY_EDITOR
        /// <summary>
        /// デフォルト値で初期化（直線パス）
        /// Initialize with default values (straight path)
        /// </summary>
        public void SetDefault(Vector3 start, Vector3 end)
        {
            startPoint = start;
            endPoint = end;

            // 制御点を始点と終点の間に配置
            // Place control points between start and end
            Vector3 direction = (end - start).normalized;
            float distance = Vector3.Distance(start, end);
            float controlDistance = distance * 0.3f;

            startControlPoint = direction * controlDistance;
            endControlPoint = -direction * controlDistance;
        }

        public void SetStartPoint(Vector3 value) => startPoint = value;
        public void SetStartControlPoint(Vector3 value) => startControlPoint = value;
        public void SetEndControlPoint(Vector3 value) => endControlPoint = value;
        public void SetEndPoint(Vector3 value) => endPoint = value;
#endif
    }
}
