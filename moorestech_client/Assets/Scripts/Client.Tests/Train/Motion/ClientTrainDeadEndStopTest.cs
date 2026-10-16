using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Train.Motion
{
    public class ClientTrainDeadEndStopTest
    {
        [TestCase(4, 12d, 1)]
        [TestCase(5, 12d, 0)]
        [TestCase(6, 0d, 0)]
        public void DeadEndMatchesServerMovementBoundary(int distance, double expectedSpeed, int expectedRemaining)
        {
            // 終端まで5の経路で到達前・一致・超過を分ける
            // Distinguish before, exact, and overshooting arrival five units before the end.
            var unit = CreateUnit(false);
            var moved = unit.UpdateTrainByDistance(distance);

            Assert.That(moved, Is.EqualTo(Math.Min(distance, 5)));
            Assert.That(unit.CurrentSpeed, Is.EqualTo(expectedSpeed));
            Assert.That(unit.AccumulatedDistance, Is.EqualTo(0.375d));
            Assert.That(unit.RailPosition.DistanceToNextNode, Is.EqualTo(expectedRemaining));
        }

        [Test]
        public void ExactArrivalStopsOnlyOnNextAttemptToMove()
        {
            var unit = CreateUnit(false);
            unit.UpdateTrainByDistance(5);
            Assert.That(unit.CurrentSpeed, Is.EqualTo(12d));

            // 端での移動ゼロでは止めず、超過した時だけ止める
            // Keep speed on a zero-distance step and stop only when movement exceeds the end.
            unit.UpdateTrainByDistance(0);
            Assert.That(unit.CurrentSpeed, Is.EqualTo(12d));
            Assert.That(unit.UpdateTrainByDistance(1), Is.Zero);
            Assert.That(unit.CurrentSpeed, Is.Zero);
            Assert.That(unit.AccumulatedDistance, Is.EqualTo(0.375d));
        }

        [Test]
        public void ConnectedTargetDoesNotBecomeDeadEndStop()
        {
            var unit = CreateUnit(true);
            LogAssert.Expect(LogType.Warning, "1st hashよりApplySnapshotTrainUnitのtickが前ならこれは想定内です。次のhash検証でmismatchになる可能性あり");

            // 未通知の進路がある場合は既存の警告経路を保つ
            // Preserve the existing warning path when an onward connection exists.
            unit.UpdateTrainByDistance(6);
            Assert.That(unit.CurrentSpeed, Is.EqualTo(12d));
            Assert.That(unit.AccumulatedDistance, Is.EqualTo(0.375d));
        }

        [TestCase(5)]
        [TestCase(6)]
        public void DockingStillClearsSpeedAndFractionalDistance(int distance)
        {
            var unit = CreateUnit(false);
            unit.ApplyPreSimulationDiff(0, true, -1, false, 0);

            // 通知済みドッキングは行き止まり停止より優先する
            // A notified docking stop takes precedence over dead-end handling.
            unit.UpdateTrainByDistance(distance);
            Assert.That(unit.CurrentSpeed, Is.Zero);
            Assert.That(unit.AccumulatedDistance, Is.Zero);
        }

        [Test]
        public void OvershootHashMatchesServerStoppedState()
        {
            var unit = CreateUnit(false);
            unit.UpdateTrainByDistance(6);
            Assert.That(unit.TryCreateSnapshotBundle(out var actual), Is.True);

            // サーバーの速度ゼロ・端数保持状態とハッシュを比較する
            // Compare the hash with the server's zero-speed state preserving the fractional distance.
            var expectedSimulation = new TrainSimulationSnapshot(unit.TrainUnitInstanceId, 0d, 0.375d, 0, 0, Array.Empty<TrainCarSnapshot>());
            var expected = new TrainUnitSnapshotBundle(expectedSimulation, actual.RailPositionSnapshot);
            Assert.That(TrainUnitSnapshotHashCalculator.Compute(new[] { actual }),
                Is.EqualTo(TrainUnitSnapshotHashCalculator.Compute(new[] { expected })));
        }

        private static ClientTrainUnit CreateUnit(bool hasOutgoingConnection)
        {
            // 既存プレビューテストの直線ノードを再利用する
            // Reuse the straight rail nodes from existing preview tests.
            var start = new PlacementTestRailNode(0, Vector3.zero);
            var end = new PlacementTestRailNode(1, Vector3.forward);
            start.ConnectTo(end, 100);
            if (hasOutgoingConnection)
            {
                end.ConnectTo(new PlacementTestRailNode(2, Vector3.forward * 2), 100);
            }

            // 公開スナップショット入口から本番の移動処理を準備する
            // Initialize production movement through the public snapshot entry point.
            var unit = new ClientTrainUnit(TrainUnitInstanceId.Create(), new TwoNodeGraph(start, end));
            var position = new RailPosition(new List<IRailNode> { end, start }, 10, 5);
            var simulation = new TrainSimulationSnapshot(unit.TrainUnitInstanceId, 12d, 0.375d, 0, 0, Array.Empty<TrainCarSnapshot>());
            unit.SnapshotUpdate(simulation, position.CreateSaveSnapshot());
            return unit;
        }

        private sealed class TwoNodeGraph : IRailGraphProvider
        {
            private readonly Dictionary<ConnectionDestination, IRailNode> _nodes;

            internal TwoNodeGraph(IRailNode start, IRailNode end)
            {
                _nodes = new Dictionary<ConnectionDestination, IRailNode>
                {
                    { start.ConnectionDestination, start },
                    { end.ConnectionDestination, end },
                };
            }

            public IRailNode ResolveRailNode(ConnectionDestination destination) => _nodes[destination];
            public IReadOnlyList<IRailNode> FindShortestPath(IRailNode start, IRailNode end) => throw new NotSupportedException();
            public int GetDistance(IRailNode start, IRailNode end, bool useFindPath) => start.GetDistanceToNode(end, useFindPath);
        }
    }
}
