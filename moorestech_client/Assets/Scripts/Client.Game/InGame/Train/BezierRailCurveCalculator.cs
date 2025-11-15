using Game.Train.Utility;
using Game.Common.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    internal static class BezierRailCurveCalculator
    {
        private const float MinimumTangentLength = 0.05f;
        private const float ShortDistanceThreshold = 0.5f;
        
        public static bool ValidateRailConnection(RailNodeInfoMessagePack fromNode, RailNodeInfoMessagePack toNode)
        {
            // 制御点が存在するか簡易チェック
            // Ensure both control points exist before calculation
            if (fromNode == null || toNode == null) return false;
            if (fromNode.ControlPoint == null || toNode.ControlPoint == null) return false;
            return true;
        }
        
        public static (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) CalculateBezierControlPoints(RailNodeInfoMessagePack fromNode, RailNodeInfoMessagePack toNode, int distance)
        {
            // メッセージパックからベジエ制御点を算出
            // Convert message pack data into bezier control points
            var startPosition = EnsureFinite((Vector3)fromNode.ControlPoint.OriginalPosition);
            var startOffset = EnsureFinite((Vector3)fromNode.ControlPoint.ControlPointPosition);
            var endPosition = EnsureFinite((Vector3)toNode.ControlPoint.OriginalPosition);
            var endOffset = EnsureFinite((Vector3)toNode.ControlPoint.ControlPointPosition);
            
            var p0 = startPosition;
            var p1 = startPosition + startOffset;
            var p3 = endPosition;
            var p2 = endPosition + endOffset;
            
            var worldDistance = Mathf.Abs(distance) / BezierUtility.RAIL_LENGTH_SCALE;
            var clampLength = Mathf.Max(worldDistance * 0.5f, MinimumTangentLength);
            if (worldDistance < ShortDistanceThreshold) clampLength = Mathf.Max(worldDistance * 0.3f, MinimumTangentLength * 0.5f);
            
            var tangentOut = ClampVector(p1 - p0, clampLength);
            var tangentIn = ClampVector(p2 - p3, clampLength);
            
            p1 = p0 + tangentOut;
            p2 = p3 + tangentIn;
            
            return (p0, p1, p2, p3);
        }
        
        private static Vector3 EnsureFinite(Vector3 value)
        {
            // NaN や Infinity をゼロに置き換え
            // Replace NaN or Infinity with zero vector
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)) return Vector3.zero;
            if (float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z)) return Vector3.zero;
            return value;
        }
        
        private static Vector3 ClampVector(Vector3 value, float maxLength)
        {
            // ベクトル長を制限して過剰な曲率を抑制
            // Clamp vector magnitude to avoid excessive curvature
            if (value == Vector3.zero) return value;
            var sqrLimit = maxLength * maxLength;
            return value.sqrMagnitude <= sqrLimit ? value : value.normalized * maxLength;
        }
    }
}
