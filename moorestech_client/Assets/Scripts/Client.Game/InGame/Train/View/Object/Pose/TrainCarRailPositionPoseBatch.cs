using System;
using System.Collections.Generic;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object.Pose
{
    public sealed class TrainCarRailPositionPoseBatch
    {
        private const int ArcLengthSamples = 64;
        private const float MinCurveLength = 1e-4f;
        private const int MaxArcLengthCacheEntries = 4096;

        private readonly List<int> _offsets = new();
        private readonly Dictionary<int, int> _offsetToPointIndex = new();
        private readonly List<PosePoint> _posePoints = new();
        private readonly Dictionary<ArcLengthCacheKey, ArcLengthCacheEntry> _arcLengthCache = new();

        private RailPosition _railPosition;
        private bool _isResolved;

        public void Begin(RailPosition railPosition)
        {
            // 前回フレームの要求だけを消し、レール形状キャッシュは再利用する
            // Clear per-frame requests while keeping rail-curve caches reusable
            _railPosition = railPosition;
            _isResolved = false;
            _offsets.Clear();
            _offsetToPointIndex.Clear();
            _posePoints.Clear();
        }

        public bool RequestPose(TrainCarRailPositionVisualState visualState)
        {
            if (_isResolved || visualState.RailPosition != _railPosition)
            {
                return false;
            }

            // 1つの表示姿勢は前端と後端の2点だけを要求する
            // One visual pose needs only its front and rear rail points
            return RequestOffset(visualState.FrontOffset) && RequestOffset(visualState.RearOffset);
        }

        public bool TryResolve()
        {
            if (_railPosition == null)
            {
                return false;
            }
            if (_isResolved)
            {
                return true;
            }

            // 全要求offsetを昇順に並べ、RailPositionを先頭から1回だけ走査する
            // Sort all requested offsets and scan the RailPosition only once from the head
            _offsets.Sort();
            RebuildOffsetIndex();
            var resolved = TryResolveSortedOffsets();
            _isResolved = resolved;
            return resolved;
        }

        public bool TryGetPose(TrainCarRailPositionVisualState visualState, float modelForwardCenterOffset, out TrainCarPoseResult pose)
        {
            pose = default;
            if (!_isResolved || visualState.RailPosition != _railPosition)
            {
                return false;
            }
            if (!_offsetToPointIndex.TryGetValue(visualState.FrontOffset, out var frontIndex) ||
                !_offsetToPointIndex.TryGetValue(visualState.RearOffset, out var rearIndex))
            {
                return false;
            }

            // 解決済みの前後端点から中心位置と向きを作る
            // Build the center pose from already resolved front and rear points
            var front = _posePoints[frontIndex];
            var rear = _posePoints[rearIndex];
            var position = (front.Position + rear.Position) * 0.5f;
            var delta = front.Position - rear.Position;
            var forward = delta.sqrMagnitude > 1e-6f ? delta.normalized : front.Forward;

            // 既存のモデル軸補正とrenderer中心補正は単体pose解決と同じにする
            // Keep model-axis and renderer-center corrections identical to single-pose resolution
            var rotation = TrainCarPoseCalculator.BuildRotation(forward, visualState.IsFacingForward);
            var modelForward = TrainCarPoseCalculator.ResolveModelForward(rotation);
            position -= modelForward * modelForwardCenterOffset;
            pose = new TrainCarPoseResult(position, rotation);
            return true;
        }

        private bool RequestOffset(int offset)
        {
            if (offset < 0)
            {
                return false;
            }
            if (_offsetToPointIndex.ContainsKey(offset))
            {
                return true;
            }

            // sort前は仮indexを入れ、Resolve時に昇順indexへ作り直す
            // Store a temporary index before sorting and rebuild sorted indices on resolve
            _offsetToPointIndex.Add(offset, _offsets.Count);
            _offsets.Add(offset);
            return true;
        }

        private void RebuildOffsetIndex()
        {
            _offsetToPointIndex.Clear();
            _posePoints.Clear();
            for (var i = 0; i < _offsets.Count; i++)
            {
                _offsetToPointIndex.Add(_offsets[i], i);
                _posePoints.Add(default);
            }
        }

        private bool TryResolveSortedOffsets()
        {
            if (_offsets.Count == 0)
            {
                return true;
            }

            // node列と先頭セグメント上の進み量から、各offsetが属するsegmentを順に探す
            // Walk segments once and resolve each sorted offset on the segment that contains it
            var railNodes = _railPosition.GetRailNodes();
            if (railNodes == null || railNodes.Count < 2)
            {
                return false;
            }

            var distanceToNext = _railPosition.GetDistanceToNextNode();
            var segmentIndex = 0;
            var consumedLength = 0;
            for (var offsetIndex = 0; offsetIndex < _offsets.Count; offsetIndex++)
            {
                if (!TryResolveOffset(railNodes, distanceToNext, ref segmentIndex, ref consumedLength, _offsets[offsetIndex], out var posePoint))
                {
                    return false;
                }
                _posePoints[offsetIndex] = posePoint;
            }
            return true;
        }

        private bool TryResolveOffset(IReadOnlyList<IRailNode> railNodes, int distanceToNext, ref int segmentIndex, ref int consumedLength, int targetOffset, out PosePoint posePoint)
        {
            posePoint = default;
            while (segmentIndex < railNodes.Count - 1)
            {
                var ahead = railNodes[segmentIndex];
                var behind = railNodes[segmentIndex + 1];
                var segmentLength = behind.GetDistanceToNode(ahead);
                if (segmentLength < 0)
                {
                    return false;
                }
                if (segmentLength == 0)
                {
                    segmentIndex++;
                    continue;
                }

                // 先頭segmentだけは列車先頭より前の残り距離を除外する
                // Exclude the portion ahead of the train head only on the head segment
                var availableLength = segmentIndex == 0 ? segmentLength - distanceToNext : segmentLength;
                if (availableLength < 0)
                {
                    return false;
                }
                if (availableLength == 0)
                {
                    segmentIndex++;
                    continue;
                }

                if (targetOffset <= consumedLength + availableLength)
                {
                    var remainingOnSegment = targetOffset - consumedLength;
                    var distanceFromBehind = segmentIndex == 0 ? availableLength - remainingOnSegment : segmentLength - remainingOnSegment;
                    return TryResolvePoseOnSegment(behind, ahead, distanceFromBehind, out posePoint);
                }

                consumedLength += availableLength;
                segmentIndex++;
            }
            return false;
        }

        private bool TryResolvePoseOnSegment(IRailNode behind, IRailNode ahead, int distanceFromBehind, out PosePoint posePoint)
        {
            posePoint = default;
            if (behind == null || ahead == null || distanceFromBehind < 0)
            {
                return false;
            }

            // segment形状ごとの弧長テーブルを再利用してdistanceからBezier tへ変換する
            // Reuse per-segment arc-length tables to convert distance into Bezier t
            BezierUtility.BuildRenderControlPoints(behind.FrontControlPoint, ahead.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            var arcLengthCache = GetArcLengthCache(p0, p1, p2, p3);
            var distanceWorld = distanceFromBehind / BezierUtility.RAIL_LENGTH_SCALE;
            var delta = p3 - p0;
            var t = arcLengthCache.CurveLength > MinCurveLength
                ? BezierUtility.DistanceToTime(distanceWorld, arcLengthCache.CurveLength, arcLengthCache.ArcLengths)
                : ComputeLinearT(distanceWorld, delta);

            // positionとtangentをまとめて保存し、各visual spanの姿勢計算で再利用する
            // Store position and tangent once for reuse by visual-span pose composition
            var position = BezierUtility.GetBezierPoint(p0, p1, p2, p3, t);
            var tangent = BezierUtility.GetBezierTangent(p0, p1, p2, p3, t);
            var forward = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : (delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.forward);
            posePoint = new PosePoint(position, forward);
            return true;
        }

        private ArcLengthCacheEntry GetArcLengthCache(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var key = new ArcLengthCacheKey(p0, p1, p2, p3, ArcLengthSamples);
            if (_arcLengthCache.TryGetValue(key, out var entry))
            {
                return entry;
            }

            // レール編集で形状が増え続けても、batch側のcacheを一定量で打ち切る
            // Bound the batch-side cache so rail edits cannot grow it without limit
            if (_arcLengthCache.Count >= MaxArcLengthCacheEntries)
            {
                _arcLengthCache.Clear();
            }
            var arcLengths = Array.Empty<float>();
            var curveLength = BezierUtility.BuildArcLengthTable(p0, p1, p2, p3, ArcLengthSamples, ref arcLengths);
            entry = new ArcLengthCacheEntry(curveLength, arcLengths);
            _arcLengthCache.Add(key, entry);
            return entry;
        }

        private static float ComputeLinearT(float distanceWorld, Vector3 delta)
        {
            var straightLength = Mathf.Max(MinCurveLength, delta.magnitude);
            return Mathf.Clamp01(distanceWorld / straightLength);
        }

        private readonly struct PosePoint
        {
            public readonly Vector3 Position;
            public readonly Vector3 Forward;

            public PosePoint(Vector3 position, Vector3 forward)
            {
                Position = position;
                Forward = forward;
            }
        }

        private readonly struct ArcLengthCacheEntry
        {
            public readonly float CurveLength;
            public readonly float[] ArcLengths;

            public ArcLengthCacheEntry(float curveLength, float[] arcLengths)
            {
                CurveLength = curveLength;
                ArcLengths = arcLengths;
            }
        }

        private readonly struct ArcLengthCacheKey : IEquatable<ArcLengthCacheKey>
        {
            private readonly Vector3 _p0;
            private readonly Vector3 _p1;
            private readonly Vector3 _p2;
            private readonly Vector3 _p3;
            private readonly int _samples;

            public ArcLengthCacheKey(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples)
            {
                _p0 = p0;
                _p1 = p1;
                _p2 = p2;
                _p3 = p3;
                _samples = samples;
            }

            public bool Equals(ArcLengthCacheKey other)
            {
                return _samples == other._samples &&
                       _p0.Equals(other._p0) &&
                       _p1.Equals(other._p1) &&
                       _p2.Equals(other._p2) &&
                       _p3.Equals(other._p3);
            }

            public override bool Equals(object obj)
            {
                return obj is ArcLengthCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _samples;
                    hash = (hash * 397) ^ _p0.GetHashCode();
                    hash = (hash * 397) ^ _p1.GetHashCode();
                    hash = (hash * 397) ^ _p2.GetHashCode();
                    hash = (hash * 397) ^ _p3.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
