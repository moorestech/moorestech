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

        // 列車モデルの前方軸補正をレール進行方向に合わせる
        // Model forward axis correction to match rail direction
        public const float ModelYawOffsetDegrees = -90f;

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

            // 先頭から最後尾方向へ、有効な描画セグメントだけで距離を評価する
            // Evaluate distance from head to rear using only drawable-length segments.
            var distanceToNext = railPosition.GetDistanceToNextNode();
            var remaining = distanceFromHead;
            for (var i = 0; i < railNodes.Count - 1; i++)
            {
                var ahead = railNodes[i];
                var behind = railNodes[i + 1];
                var segmentLength = behind.GetDistanceToNode(ahead);
                if (segmentLength < 0) return false;
                if (segmentLength == 0) continue;

                // 先頭セグメントだけは、列車先頭が既に進んだぶんを除外する
                // For the head segment, exclude the portion still ahead of the train head.
                var availableLength = i == 0 ? segmentLength - distanceToNext : segmentLength;
                if (availableLength < 0) return false;
                if (availableLength == 0) continue;

                // 対象距離がこのセグメント内なら、後方ノードからの距離に変換して姿勢を返す
                // If the target lies on this segment, convert it to distance from the rear node.
                if (remaining <= availableLength)
                {
                    var distanceFromBehind = i == 0 ? availableLength - remaining : segmentLength - remaining;
                    return TryGetPoseOnSegment(behind, ahead, distanceFromBehind, out position, out forward);
                }
                remaining -= availableLength;
            }

            return false;
        }

        // 前後 offset から車両または表示 part の中心姿勢を計算する
        // Compute a car or visual-part center pose from front/rear offsets
        public static bool TryResolveCarPose(RailPosition railPosition, int frontOffset, int rearOffset, out Vector3 position, out Vector3 forward)
        {
            // 出力値を先に初期化する
            // Initialize output values first
            position = default;
            forward = Vector3.forward;
            if (!TryGetPose(railPosition, frontOffset, out var frontPosition, out var frontForward))
            {
                return false;
            }

            // 後端位置を解決し、前後点の中央を表示中心にする
            // Resolve the rear point and use the midpoint as the visual center
            if (!TryGetPose(railPosition, rearOffset, out var rearPosition, out _))
            {
                return false;
            }
            position = (frontPosition + rearPosition) * 0.5f;

            // 前後差分が退化する場合は先頭側 tangent を fallback に使う
            // Fall back to the head-side tangent when front/rear delta degenerates
            var delta = frontPosition - rearPosition;
            forward = delta.sqrMagnitude > 1e-6f ? delta.normalized : (frontForward.sqrMagnitude > 1e-6f ? frontForward.normalized : Vector3.forward);
            return true;
        }

        // renderer 中心補正込みの最終 Transform 姿勢を計算する
        // Compute the final Transform pose including renderer-center correction
        public static bool TryResolveRenderPose(RailPosition railPosition, int frontOffset, int rearOffset, bool isFacingForward, float modelForwardCenterOffset, out Vector3 position, out Quaternion rotation)
        {
            // 出力値を先に初期化する
            // Initialize output values first
            position = default;
            rotation = Quaternion.identity;
            if (!TryResolveCarPose(railPosition, frontOffset, rearOffset, out position, out var forward))
            {
                return false;
            }

            // モデル軸補正と中心 offset を適用する
            // Apply model-axis correction and center offset
            rotation = BuildRotation(forward, isFacingForward);
            var modelForward = ResolveModelForward(rotation);
            position -= modelForward * modelForwardCenterOffset;
            return true;
        }

        // 正規化した前方ベクトルから回転を構成する
        // Build rotation from the normalized forward vector
        public static Quaternion BuildRotation(Vector3 forward, bool isFacingForward)
        {
            var safeForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            var rotation = Quaternion.LookRotation(safeForward, Vector3.up);

            // モデル軸補正と編成向き反転を適用する
            // Apply model axis correction and formation facing inversion
            rotation = rotation * Quaternion.Euler(0f, ModelYawOffsetDegrees, 0f);
            if (!isFacingForward)
            {
                rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            }

            return rotation;
        }

        // 現在 rotation におけるモデル前方軸を求める
        // Resolve the model forward axis under the current rotation
        public static Vector3 ResolveModelForward(Quaternion rotation)
        {
            var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
            return rotation * localForwardAxis;
        }

        private static bool TryGetPoseOnSegment(IRailNode behind, IRailNode ahead, int distanceFromBehind, out Vector3 position, out Vector3 forward)
        {
            // 出力を初期化する
            // Initialize output values
            position = default;
            forward = Vector3.forward;

            // セグメント入力を検証する
            // Validate segment inputs
            if (behind == null || ahead == null || distanceFromBehind < 0) return false;

            // 描画用の制御点を使ってセグメントの位置を計算する
            // Build render control points for the segment
            BezierUtility.BuildRenderControlPoints(behind.FrontControlPoint, ahead.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
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


    }
}
