using System;
using System.Collections.Generic;
using Game.Train.RailGraph;

namespace Game.Train.RailPositions
{
    public static class RailPositionOverlapDetector
    {
        public sealed class OverlapIndex
        {
            // 物理レール区間ID(=RailObjectId相当)ごとに占有距離区間を保持する
            // Store occupied distance intervals per physical rail segment id (RailObjectId equivalent)
            private readonly Dictionary<ulong, List<SegmentInterval>> _intervalsBySegment;

            internal OverlapIndex(Dictionary<ulong, List<SegmentInterval>> intervalsBySegment)
            {
                _intervalsBySegment = intervalsBySegment ?? new Dictionary<ulong, List<SegmentInterval>>();
            }

            internal Dictionary<ulong, List<SegmentInterval>> GetIntervalsBySegment()
            {
                return _intervalsBySegment;
            }
        }

        public static bool HasOverlap(RailPosition first, RailPosition second)
        {
            if (first == null || second == null) return false;
            var firstMap = CreateIntervalMap(first);
            if (firstMap.Count == 0) return false;
            var secondMap = CreateIntervalMap(second);
            if (secondMap.Count == 0) return false;
            return HasOverlap(firstMap, secondMap);
        }

        public static bool HasOverlap(RailPosition single, IReadOnlyList<RailPosition> many)
        {
            if (single == null || many == null || many.Count == 0) return false;
            var index = CreateIndex(many);
            return HasOverlap(single, index);
        }

        public static bool HasOverlap(IReadOnlyList<RailPosition> leftMany, IReadOnlyList<RailPosition> rightMany)
        {
            if (leftMany == null || rightMany == null || leftMany.Count == 0 || rightMany.Count == 0) return false;
            var leftIndex = CreateIndex(leftMany);
            var rightIndex = CreateIndex(rightMany);
            return HasOverlap(leftIndex, rightIndex);
        }

        // many側の比較前処理キャッシュを作る。繰り返し判定時に再利用して高速化する
        // Build a preprocessed cache for many-side overlap checks; reuse it for repeated queries
        // 注意: manyの内容が変わったらindexは無効なので必ず再生成する
        // Note: Rebuild the index whenever the source many collection changes
        public static OverlapIndex CreateIndex(IReadOnlyList<RailPosition> many)
        {
            var intervalsBySegment = new Dictionary<ulong, List<SegmentInterval>>();
            if (many == null || many.Count == 0)
            {
                return new OverlapIndex(intervalsBySegment);
            }

            for (var i = 0; i < many.Count; i++)
            {
                AppendPositionIntervals(many[i], intervalsBySegment);
            }
            NormalizeIntervals(intervalsBySegment);
            return new OverlapIndex(intervalsBySegment);
        }

        public static bool HasOverlap(RailPosition single, OverlapIndex manyIndex)
        {
            if (single == null || manyIndex == null) return false;
            var singleMap = CreateIntervalMap(single);
            if (singleMap.Count == 0) return false;
            return HasOverlap(singleMap, manyIndex.GetIntervalsBySegment());
        }

        public static bool HasOverlap(OverlapIndex leftIndex, OverlapIndex rightIndex)
        {
            if (leftIndex == null || rightIndex == null) return false;
            return HasOverlap(leftIndex.GetIntervalsBySegment(), rightIndex.GetIntervalsBySegment());
        }

        private static Dictionary<ulong, List<SegmentInterval>> CreateIntervalMap(RailPosition position)
        {
            var intervalsBySegment = new Dictionary<ulong, List<SegmentInterval>>();
            AppendPositionIntervals(position, intervalsBySegment);
            NormalizeIntervals(intervalsBySegment);
            return intervalsBySegment;
        }

        private static void AppendPositionIntervals(RailPosition position, Dictionary<ulong, List<SegmentInterval>> intervalsBySegment)
        {
            // 長さ0以下のRailPositionは占有なしとして扱うため、1:1/1:多/多:多の全判定で非衝突になる
            // RailPosition with zero-or-negative length is treated as non-occupying, so all overlap modes return non-collision for it
            if (position == null || position.TrainLength <= 0 || intervalsBySegment == null) return;

            var railNodes = position.GetRailNodes();
            if (railNodes == null || railNodes.Count < 2) return;

            var remainingLength = position.TrainLength;
            var firstSegmentStart = position.GetDistanceToNextNode();

            for (var i = 0; i < railNodes.Count - 1; i++)
            {
                if (remainingLength <= 0) break;

                var frontNode = railNodes[i];
                var rearNode = railNodes[i + 1];
                if (frontNode == null || rearNode == null) continue;

                var segmentLength = rearNode.GetDistanceToNode(frontNode);
                if (segmentLength <= 0) continue;

                var localStart = i == 0 ? Clamp(firstSegmentStart, 0, segmentLength) : 0;
                var availableLength = segmentLength - localStart;
                if (availableLength <= 0) continue;

                var occupiedLength = Math.Min(availableLength, remainingLength);
                if (occupiedLength <= 0) continue;

                var localEnd = localStart + occupiedLength;
                // keyは「表裏を同一視した物理区間ID」、start/endはその正規化向きに統一する
                // key is the physical segment id normalized across front/back directions, and start/end are aligned to that canonical axis
                var key = ComputePhysicalRailObjectId(frontNode, rearNode, out var shouldReverseAxis);
                var normalizedStart = shouldReverseAxis ? segmentLength - localEnd : localStart;
                var normalizedEnd = shouldReverseAxis ? segmentLength - localStart : localEnd;
                AddInterval(intervalsBySegment, key, normalizedStart, normalizedEnd);
                remainingLength -= occupiedLength;
            }
        }

        private static void NormalizeIntervals(Dictionary<ulong, List<SegmentInterval>> intervalsBySegment)
        {
            foreach (var pair in intervalsBySegment)
            {
                var intervals = pair.Value;
                if (intervals == null || intervals.Count <= 1) continue;

                intervals.Sort(SegmentIntervalComparer.Instance);
                var writeIndex = 0;
                var current = intervals[0];
                for (var readIndex = 1; readIndex < intervals.Count; readIndex++)
                {
                    var next = intervals[readIndex];
                    if (next.Start <= current.End)
                    {
                        current = new SegmentInterval(current.Start, Math.Max(current.End, next.End));
                        continue;
                    }
                    intervals[writeIndex] = current;
                    writeIndex++;
                    current = next;
                }
                intervals[writeIndex] = current;
                var removeCount = intervals.Count - (writeIndex + 1);
                if (removeCount > 0)
                {
                    intervals.RemoveRange(writeIndex + 1, removeCount);
                }
            }
        }

        private static bool HasOverlap(
            Dictionary<ulong, List<SegmentInterval>> leftIntervalsBySegment,
            Dictionary<ulong, List<SegmentInterval>> rightIntervalsBySegment)
        {
            if (leftIntervalsBySegment == null || rightIntervalsBySegment == null) return false;
            if (leftIntervalsBySegment.Count == 0 || rightIntervalsBySegment.Count == 0) return false;

            if (leftIntervalsBySegment.Count > rightIntervalsBySegment.Count)
            {
                var temp = leftIntervalsBySegment;
                leftIntervalsBySegment = rightIntervalsBySegment;
                rightIntervalsBySegment = temp;
            }

            foreach (var pair in leftIntervalsBySegment)
            {
                if (!rightIntervalsBySegment.TryGetValue(pair.Key, out var rightIntervals)) continue;
                if (HasOverlap(pair.Value, rightIntervals)) return true;
            }
            return false;
        }

        private static bool HasOverlap(List<SegmentInterval> leftIntervals, List<SegmentInterval> rightIntervals)
        {
            if (leftIntervals == null || rightIntervals == null) return false;
            if (leftIntervals.Count == 0 || rightIntervals.Count == 0) return false;

            var leftIndex = 0;
            var rightIndex = 0;
            while (leftIndex < leftIntervals.Count && rightIndex < rightIntervals.Count)
            {
                var left = leftIntervals[leftIndex];
                var right = rightIntervals[rightIndex];
                // [start,end) の半開区間として比較するため、端点一致は非重複とみなす
                // Intervals are compared as half-open [start,end), so touching endpoints are treated as non-overlap
                if (left.End <= right.Start)
                {
                    leftIndex++;
                    continue;
                }
                if (right.End <= left.Start)
                {
                    rightIndex++;
                    continue;
                }
                return true;
            }
            return false;
        }

        private static void AddInterval(
            Dictionary<ulong, List<SegmentInterval>> intervalsBySegment,
            ulong key,
            int start,
            int end)
        {
            if (end <= start) return;
            if (!intervalsBySegment.TryGetValue(key, out var intervals))
            {
                intervals = new List<SegmentInterval>();
                intervalsBySegment.Add(key, intervals);
            }
            intervals.Add(new SegmentInterval(start, end));
        }

        private static ulong ComputePhysicalRailObjectId(IRailNode frontNode, IRailNode rearNode, out bool shouldReverseAxis)
        {
            var forwardFromNodeId = ResolveNodeId(frontNode);
            var forwardToNodeId = ResolveNodeId(rearNode);
            var reverseFromNodeId = ResolveOppositeNodeId(rearNode);
            var reverseToNodeId = ResolveOppositeNodeId(frontNode);
            // (from,to) と (to^1,from^1) のうち辞書順で小さい方を正規形として採用する
            // Choose the lexicographically smaller pair between (from,to) and (to^1,from^1) as the canonical form
            if (IsCanonicalPair(forwardFromNodeId, forwardToNodeId, reverseFromNodeId, reverseToNodeId))
            {
                shouldReverseAxis = false;
                return ComposeRailObjectId(forwardFromNodeId, forwardToNodeId);
            }
            shouldReverseAxis = true;
            return ComposeRailObjectId(reverseFromNodeId, reverseToNodeId);
        }

        private static bool IsCanonicalPair(int fromNodeId, int toNodeId, int alternateFromNodeId, int alternateToNodeId)
        {
            if (fromNodeId < alternateFromNodeId) return true;
            if (fromNodeId > alternateFromNodeId) return false;
            return toNodeId <= alternateToNodeId;
        }

        private static ulong ComposeRailObjectId(int canonicalFrom, int canonicalTo)
        {
            return (ulong)(uint)canonicalFrom + ((ulong)(uint)canonicalTo << 32);
        }

        private static int ResolveNodeId(IRailNode node)
        {
            if (node == null) return int.MinValue;
            var nodeId = node.NodeId;
            if (nodeId >= 0) return nodeId;
            return node.NodeGuid.GetHashCode();
        }

        private static int ResolveOppositeNodeId(IRailNode node)
        {
            if (node == null) return int.MaxValue;
            var oppositeNodeId = node.OppositeNodeId;
            if (oppositeNodeId >= 0) return oppositeNodeId;
            var oppositeNode = node.OppositeNode;
            if (oppositeNode != null) return ResolveNodeId(oppositeNode);
            var fallback = ResolveNodeId(node);
            return unchecked(fallback ^ int.MinValue);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        internal readonly struct SegmentInterval
        {
            // canonicalFrom を0とする区間軸での半開区間 [Start, End)
            // Half-open interval [Start, End) on the canonical axis where canonicalFrom is 0
            public int Start { get; }
            public int End { get; }

            public SegmentInterval(int start, int end)
            {
                Start = start;
                End = end;
            }
        }

        private sealed class SegmentIntervalComparer : IComparer<SegmentInterval>
        {
            public static readonly SegmentIntervalComparer Instance = new SegmentIntervalComparer();

            public int Compare(SegmentInterval x, SegmentInterval y)
            {
                if (x.Start < y.Start) return -1;
                if (x.Start > y.Start) return 1;
                if (x.End < y.End) return -1;
                if (x.End > y.End) return 1;
                return 0;
            }
        }
    }
}
