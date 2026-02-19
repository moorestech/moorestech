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
            }
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
