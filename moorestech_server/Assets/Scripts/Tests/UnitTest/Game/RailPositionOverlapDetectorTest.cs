using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class RailPositionOverlapDetectorTest
    {
        [Test]
        public void HasOverlap_SingleToSingle_WithReversedOccupancy_ReturnsTrue()
        {
            var (first, reversed, separated) = CreateSamplePositions();
            Assert.IsTrue(RailPositionOverlapDetector.HasOverlap(first, reversed));
            Assert.IsTrue(RailPositionOverlapDetector.HasOverlap(reversed, first));
        }

        [Test]
        public void HasOverlap_SingleToSingle_DifferentSegment_ReturnsFalse()
        {
            var (first, reversed, separated) = CreateSamplePositions();
            Assert.IsFalse(RailPositionOverlapDetector.HasOverlap(first, separated));
            Assert.IsFalse(RailPositionOverlapDetector.HasOverlap(reversed, separated));
            Assert.IsFalse(RailPositionOverlapDetector.HasOverlap(separated, first));
            Assert.IsFalse(RailPositionOverlapDetector.HasOverlap(separated, reversed));
        }

        [Test]
        public void HasOverlap_SingleToMany_DetectsOverlap()
        {
            var (first, reversed, separated) = CreateSamplePositions();
            var many = new List<RailPosition> { separated, reversed };
            Assert.IsTrue(RailPositionOverlapDetector.HasOverlap(first, many));

            var index = RailPositionOverlapDetector.CreateIndex(many);
            Assert.IsTrue(RailPositionOverlapDetector.HasOverlap(first, index));
        }

        [Test]
        public void HasOverlap_ManyToMany_DetectsOverlap()
        {
            var (first, reversed, separated) = CreateSamplePositions();
            var leftMany = new List<RailPosition> { separated, first };
            var rightMany = new List<RailPosition> { reversed };
            Assert.IsTrue(RailPositionOverlapDetector.HasOverlap(leftMany, rightMany));
        }

        [Test]
        public void HasOverlap_ManyToMany_WithoutOverlap_ReturnsFalse()
        {
            var (first, _, separated) = CreateSamplePositions();
            var leftMany = new List<RailPosition> { first };
            var rightMany = new List<RailPosition> { separated };
            Assert.IsFalse(RailPositionOverlapDetector.HasOverlap(leftMany, rightMany));
        }

        [Test]
        public void EnumerateUnitIntervals_ReversedRailPosition_ProducesSameNormalizedInterval()
        {
            var (first, reversed, _) = CreateSamplePositions();
            var firstIntervals = EnumerateUnitIntervals(first).ToList();
            var reversedIntervals = EnumerateUnitIntervals(reversed).ToList();

            Assert.AreEqual(1, firstIntervals.Count);
            Assert.AreEqual(1, reversedIntervals.Count);
            Assert.AreEqual(firstIntervals[0].segmentId, reversedIntervals[0].segmentId);
            Assert.AreEqual(firstIntervals[0].start, reversedIntervals[0].start);
            Assert.AreEqual(firstIntervals[0].end, reversedIntervals[0].end);
            Assert.AreEqual(firstIntervals[0].segmentLength, reversedIntervals[0].segmentLength);
        }

        [Test]
        public void EnumerateUnitIntervals_TouchingEndpoints_StaysNonOverlapping()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railGraphDatastore = environment.GetRailGraphDatastore();
            var (frontA, backA) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (frontB, backB) = RailNode.CreatePairAndRegister(railGraphDatastore);

            const int SegmentLength = 100;
            frontB.ConnectNode(frontA, SegmentLength);
            backA.ConnectNode(backB, SegmentLength);

            var left = new RailPosition(new List<IRailNode> { frontA, frontB }, 40, 10);  // [10,50)
            var right = new RailPosition(new List<IRailNode> { frontA, frontB }, 20, 50); // [50,70)

            var leftIntervals = EnumerateUnitIntervals(left).ToList();
            var rightIntervals = EnumerateUnitIntervals(right).ToList();
            Assert.AreEqual(1, leftIntervals.Count);
            Assert.AreEqual(1, rightIntervals.Count);
            Assert.AreEqual(leftIntervals[0].end, rightIntervals[0].start);

            Assert.IsFalse(HasOverlapByUnitBuffer(new List<RailPosition> { left }, new List<RailPosition> { right }));
        }

        [Test]
        public void EnumerateUnitIntervals_MultiSegmentPosition_SplitsIntervalsBySegments()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railGraphDatastore = environment.GetRailGraphDatastore();
            var (frontA, backA) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (frontB, backB) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (frontC, backC) = RailNode.CreatePairAndRegister(railGraphDatastore);

            frontB.ConnectNode(frontA, 100);
            frontC.ConnectNode(frontB, 80);
            backA.ConnectNode(backB, 100);
            backB.ConnectNode(backC, 80);

            var position = new RailPosition(new List<IRailNode> { frontA, frontB, frontC }, 120, 30);
            var intervals = EnumerateUnitIntervals(position).ToList();

            Assert.AreEqual(2, intervals.Count);
            Assert.AreEqual(30, intervals[0].start);
            Assert.AreEqual(100, intervals[0].end);
            Assert.AreEqual(100, intervals[0].segmentLength);
            Assert.AreEqual(0, intervals[1].start);
            Assert.AreEqual(50, intervals[1].end);
            Assert.AreEqual(80, intervals[1].segmentLength);
            Assert.AreEqual(120, (intervals[0].end - intervals[0].start) + (intervals[1].end - intervals[1].start));
        }

        [Test]
        public void EnumerateUnitIntervals_ZeroLengthPosition_ReturnsNoIntervals()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railGraphDatastore = environment.GetRailGraphDatastore();
            var (frontA, backA) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (frontB, backB) = RailNode.CreatePairAndRegister(railGraphDatastore);

            frontB.ConnectNode(frontA, 100);
            backA.ConnectNode(backB, 100);

            var zeroLength = new RailPosition(new List<IRailNode> { frontA, frontB }, 0, 10);
            Assert.IsEmpty(EnumerateUnitIntervals(zeroLength).ToList());
            Assert.IsFalse(HasOverlapByUnitBuffer(new List<RailPosition> { zeroLength }, new List<RailPosition>()));
        }
        
        [Test]
        public void HasOverlap_Manual1()
        {
            var rlist = CreateSamplePositions2(42, 155, 17);
            var listA = new List<RailPosition>();
            var listB = new List<RailPosition>();
            const int TryCount = 300;
            for (int i = 0; i < TryCount; i++)
            {
                listA.Clear();
                listB.Clear();
                for (int j = 0; j < rlist.Count; j++)
                {
                    if (UnityEngine.Random.Range(0, 2) == 0)
                    {
                        if (UnityEngine.Random.Range(0, 3) != 0)
                            listA.Add(rlist[j]);
                    }
                    else
                    {
                        if (UnityEngine.Random.Range(0, 3) != 0)
                            listB.Add(rlist[j]);
                    }
                }
                var check1 = RailPositionOverlapDetector.HasOverlap(listA, listB);
                var check2 = RailPositionOverlapDetector.HasOverlap(listB, listA);
                Assert.AreEqual(check1, check2);
                // ここに1刻みの厳密チェック
                var strictCheck = HasOverlapByUnitBuffer(listA, listB);
                Assert.AreEqual(check1, strictCheck);
            }
        }
        
        private static bool HasOverlapByUnitBuffer(IReadOnlyList<RailPosition> listA, IReadOnlyList<RailPosition> listB)
        {
            // A側の占有を1刻みバッファへ展開する
            // Expand side-A occupancy into per-unit buffers
            var buffers = new Dictionary<ulong, byte[]>();
            var lengths = new Dictionary<ulong, int>();
            FillBuffer(listA, 1, false);
            
            // B側を展開しながらAとの衝突を検出する
            // Detect overlap against side-A while expanding side-B
            return FillBuffer(listB, 2, true);

            #region Internal

            bool FillBuffer(IReadOnlyList<RailPosition> source, byte owner, bool detectAgainstA)
            {
                if (source == null || source.Count == 0) return false;
                for (int i = 0; i < source.Count; i++)
                {
                    foreach (var (segmentId, start, end, segmentLength) in EnumerateUnitIntervals(source[i]))
                    {
                        var buffer = GetOrCreateSegmentBuffer(segmentId, segmentLength);
                        for (int unit = start; unit < end; unit++)
                        {
                            if (detectAgainstA && buffer[unit] == 1)
                            {
                                return true;
                            }
                            if (buffer[unit] == 0)
                            {
                                buffer[unit] = owner;
                            }
                        }
                    }
                }
                return false;
            }

            byte[] GetOrCreateSegmentBuffer(ulong segmentId, int segmentLength)
            {
                if (buffers.TryGetValue(segmentId, out var existing))
                {
                    Assert.AreEqual(lengths[segmentId], segmentLength);
                    return existing;
                }

                var created = new byte[segmentLength];
                buffers[segmentId] = created;
                lengths[segmentId] = segmentLength;
                return created;
            }

            #endregion
        }

        private static IEnumerable<(ulong segmentId, int start, int end, int segmentLength)> EnumerateUnitIntervals(RailPosition position)
        {
            if (position == null || position.TrainLength <= 0) yield break;

            var railNodes = position.GetRailNodes();
            if (railNodes == null || railNodes.Count < 2) yield break;

            var remainingLength = position.TrainLength;
            var firstSegmentStart = position.GetDistanceToNextNode();
            for (int i = 0; i < railNodes.Count - 1; i++)
            {
                if (remainingLength <= 0) yield break;

                var frontNode = railNodes[i];
                var rearNode = railNodes[i + 1];
                if (frontNode == null || rearNode == null) continue;

                var segmentLength = rearNode.GetDistanceToNode(frontNode);
                if (segmentLength <= 0) continue;

                var localStart = i == 0 ? Clamp(firstSegmentStart, 0, segmentLength) : 0;
                var availableLength = segmentLength - localStart;
                if (availableLength <= 0) continue;

                var occupiedLength = Mathf.Min(availableLength, remainingLength);
                if (occupiedLength <= 0) continue;

                var localEnd = localStart + occupiedLength;
                var segmentId = ComputePhysicalRailObjectId(frontNode, rearNode, out var shouldReverseAxis);
                var normalizedStart = shouldReverseAxis ? segmentLength - localEnd : localStart;
                var normalizedEnd = shouldReverseAxis ? segmentLength - localStart : localEnd;
                if (normalizedStart < normalizedEnd)
                {
                    yield return (segmentId, normalizedStart, normalizedEnd, segmentLength);
                }

                remainingLength -= occupiedLength;
            }
        }

        private static ulong ComputePhysicalRailObjectId(IRailNode frontNode, IRailNode rearNode, out bool shouldReverseAxis)
        {
            var forwardFromNodeId = ResolveNodeId(frontNode);
            var forwardToNodeId = ResolveNodeId(rearNode);
            var reverseFromNodeId = ResolveOppositeNodeId(rearNode);
            var reverseToNodeId = ResolveOppositeNodeId(frontNode);
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

        private static (RailPosition first, RailPosition reversed, RailPosition separated) CreateSamplePositions()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railGraphDatastore = environment.GetRailGraphDatastore();

            var (aFront, aBack) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (bFront, bBack) = RailNode.CreatePairAndRegister(railGraphDatastore);
            var (cFront, cBack) = RailNode.CreatePairAndRegister(railGraphDatastore);

            const int SegmentLength = 100;
            bFront.ConnectNode(aFront, SegmentLength);
            cFront.ConnectNode(bFront, SegmentLength);
            aBack.ConnectNode(bBack, SegmentLength);
            bBack.ConnectNode(cBack, SegmentLength);

            var first = new RailPosition(new List<IRailNode> { aFront, bFront }, 40, 10);
            var reversed = first.DeepCopy();
            reversed.Reverse();
            var separated = new RailPosition(new List<IRailNode> { bFront, cFront }, 40, 10);

            return (first, reversed, separated);
        }
        
        // 負荷はnの二乗に比例,railpositionCountの1乗に比例
        private static List<RailPosition> CreateSamplePositions2(int nodePairCount, int railPositionCount, int loopLength)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railGraphDatastore = environment.GetRailGraphDatastore();
            var allNodes = new List<RailNode>();
            const int SegmentLength = 11;
            const int MaxRailPositionLength = SegmentLength * 3 + SegmentLength / 2;
            
            for (int i = 0; i < nodePairCount; i++)
            {
                var (aFront, aBack) = RailNode.CreatePairAndRegister(railGraphDatastore);
                allNodes.Add(aFront);
                allNodes.Add(aBack);
            }
            for (int i = 0; i < nodePairCount; i++)
            {
                for (int j = 0; j < nodePairCount; j++)
                {
                    if (i == j)
                        continue;
                    // Connect nodes (i to j) and (j to i)
                    allNodes[i * 2].ConnectNode(allNodes[j * 2], SegmentLength);
                    allNodes[j * 2 + 1].ConnectNode(allNodes[i * 2 + 1], SegmentLength);
                }
            }
            
            // 起点bつくる
            var (bFront, _) = RailNode.CreatePairAndRegister(railGraphDatastore);
            bFront.ConnectNode(allNodes[0], MaxRailPositionLength + 1);
            
            // 全部front側に作成
            var result = new List<RailPosition>();
            for (int i = 0; i < railPositionCount; i++)
            {
                //ランダム1～MaxRailPositionLengthの長さのRailPositionを作る
                var length = UnityEngine.Random.Range(1, MaxRailPositionLength);
                var initialRailPosition = new RailPosition(new List<IRailNode> { allNodes[0], bFront }, length, 0);
                result.Add(initialRailPosition);
            }
            // 最低でもMaxRailPositionLengthすすめる
            for (int i = 0; i < railPositionCount; i++)
            {
                var railPosition = result[i];
                var remainingLength = UnityEngine.Random.Range(MaxRailPositionLength, MaxRailPositionLength + nodePairCount * SegmentLength * loopLength + 1);
                while (remainingLength > 0)
                {
                    var movedDistance = railPosition.MoveForward(remainingLength);
                    remainingLength -= movedDistance;
                    if (movedDistance == 0)
                    {
                        // 分岐はランダムに選択
                        var nextNodes = railPosition.GetNodeApproaching().ConnectedNodes.ToList();
                        if (nextNodes.Count == 0) break;
                        var nextNodeIndex = UnityEngine.Random.Range(0, nextNodes.Count);
                        railPosition.AddNodeToHead(nextNodes[nextNodeIndex]);
                    }                
                }
            }
            for (int i = 0; i < railPositionCount; i++)
            {
                // ランダムに反転
                if (UnityEngine.Random.Range(0, 2) == 0)
                {
                    result[i].Reverse();
                }
            }
            return result;
        }
        
    }
}
