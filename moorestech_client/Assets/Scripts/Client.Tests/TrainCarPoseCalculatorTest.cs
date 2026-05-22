using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.View;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class TrainCarPoseCalculatorTest
    {
        [Test]
        public void TryGetPose_ResolvesHeadPose_WhenHeadSegmentHasZeroDistance()
        {
            // 駅同士の重なりを先頭セグメントに置き、後方の実セグメントで姿勢を補完する
            // Place the station overlap at the head segment and resolve pose from the rear real segment.
            var stationAhead = TestRailNode.Create(0, Vector3.zero);
            var stationBehind = TestRailNode.Create(1, Vector3.zero);
            var rear = TestRailNode.Create(2, new Vector3(0f, 0f, -1f));
            stationBehind.ConnectTo(stationAhead, 0);
            rear.ConnectTo(stationBehind, 1024);

            // 0距離セグメントを含むRailPositionをsnapshot復元後と同じ順序で作る
            // Build RailPosition in the same order as a restored snapshot with a zero-length segment.
            var railPosition = new RailPosition(new List<IRailNode> { stationAhead, stationBehind, rear }, 512, 0);
            var resolved = TrainCarPoseCalculator.TryGetPose(railPosition, 0, out var position, out var forward);

            // 先頭が重なりノード上にいても表示姿勢が失敗しないことを確認する
            // Verify the render pose still resolves when the head sits on overlapped nodes.
            Assert.IsTrue(resolved);
            Assert.That(Vector3.Distance(position, Vector3.zero), Is.LessThan(0.001f));
            Assert.That(forward.sqrMagnitude, Is.GreaterThan(0.5f));
        }

        [Test]
        public void TryGetPose_WalksAcrossInternalZeroDistanceSegment()
        {
            // 通常セグメントの後ろに駅重なりを置き、さらに後方セグメントへ距離を進める
            // Put a station overlap behind a normal segment and continue walking to the rear segment.
            var head = TestRailNode.Create(0, new Vector3(0f, 0f, 1f));
            var stationAhead = TestRailNode.Create(1, Vector3.zero);
            var stationBehind = TestRailNode.Create(2, Vector3.zero);
            var rear = TestRailNode.Create(3, new Vector3(0f, 0f, -1f));
            stationAhead.ConnectTo(head, 1024);
            stationBehind.ConnectTo(stationAhead, 0);
            rear.ConnectTo(stationBehind, 1024);

            // 車両後端側が0距離接続を越える位置を指定する
            // Request a pose beyond the zero-length connection on the rear side.
            var railPosition = new RailPosition(new List<IRailNode> { head, stationAhead, stationBehind, rear }, 1536, 0);
            var resolved = TrainCarPoseCalculator.TryGetPose(railPosition, 1280, out var position, out var forward);

            // 0距離接続で停止扱いにならず、後方の実セグメント上で姿勢が得られることを確認する
            // Verify the zero-length connection does not stop pose resolution before the rear segment.
            Assert.IsTrue(resolved);
            Assert.IsFalse(float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z));
            Assert.That(forward.sqrMagnitude, Is.GreaterThan(0.5f));
        }

        private sealed class TestRailNode : IRailNode
        {
            private readonly Dictionary<IRailNode, int> _distances = new();

            private TestRailNode(int nodeId, Vector3 position)
            {
                // テスト用RailNodeの識別子と制御点を固定する
                // Initialize deterministic identifiers and control points for tests.
                NodeId = nodeId;
                NodeGuid = Guid.NewGuid();
                ConnectionDestination = new ConnectionDestination(new Vector3Int(nodeId, 0, 0), 0, true);
                FrontControlPoint = new RailControlPoint(position, Vector3.forward);
                BackControlPoint = new RailControlPoint(position, Vector3.forward);
                StationRef = new StationReference();
            }

            public int NodeId { get; }
            public int OppositeNodeId => -1;
            public IRailNode OppositeNode => null;
            public ConnectionDestination ConnectionDestination { get; }
            public Guid NodeGuid { get; }
            public IRailGraphProvider GraphProvider => null;
            public StationReference StationRef { get; }
            public RailControlPoint FrontControlPoint { get; }
            public RailControlPoint BackControlPoint { get; }
            public IEnumerable<IRailNode> ConnectedNodes => _distances.Keys;
            public IEnumerable<(IRailNode node, int distance)> ConnectedNodesWithDistance
            {
                get
                {
                    foreach (var pair in _distances)
                    {
                        yield return (pair.Key, pair.Value);
                    }
                }
            }

            public static TestRailNode Create(int nodeId, Vector3 position)
            {
                return new TestRailNode(nodeId, position);
            }

            public void ConnectTo(IRailNode node, int distance)
            {
                _distances[node] = distance;
            }

            public int GetDistanceToNode(IRailNode node, bool useFindPath)
            {
                return _distances.TryGetValue(node, out var distance) ? distance : -1;
            }
        }
    }
}
