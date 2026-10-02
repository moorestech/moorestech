using Client.Game.InGame.BeltTransport;
using System;
using Core.BeltTransport;
using MessagePack;
using NUnit.Framework;
using Server.Util.MessagePack.BeltTransport;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltWireRoundTripTest
    {
        [Test]
        public void SnapshotAndEveryBoundaryVariantRoundTripTest()
        {
            // 停止・坂・バッファ・優先順を同梱。
            // Include stopped boundaries, slope entry, buffers and priority.
            var cell = new BeltNetworkCell(4, -2, 3, 7, 0, "gear:0.25:32:1", BeltDirection.Right, new BeltCellSurfaceProfile(0.1f, 1.1f));
            var item = new BeltItem(BeltTestState.Identity, 17);
            var state = new BeltCellItemState(4, 128, BeltDirection.Left, 1, item, true);
            var edge = new BeltNetworkConnection(4, 9, true, false, BeltDirection.Right, -1);
            var snapshot = new BeltCommittedSnapshot(123, new BeltNetworkSnapshot(new[] { cell }, new[] { edge }, new[] { state }, new[] { new BeltCellPriority(4, 57) }));
            var result = RoundTrip(new BeltSnapshotMessagePack(snapshot)).ToCore();
            Assert.AreEqual(snapshot.Tick, result.Tick);
            CollectionAssert.AreEqual(snapshot.Snapshot.Cells, result.Snapshot.Cells);
            CollectionAssert.AreEqual(snapshot.Snapshot.Connections, result.Snapshot.Connections);
            CollectionAssert.AreEqual(snapshot.Snapshot.Items, result.Snapshot.Items);
            CollectionAssert.AreEqual(snapshot.Snapshot.Priorities, result.Snapshot.Priorities);

            var changes = new BeltBoundaryChange[] {
                new BeltSpeedChange(new[] { new BeltCellSpeed(4, 32) }), new BeltInputChange(4, BeltDirection.Left, 12, item),
                new BeltCellItemsChange(4, new[] { state }), new BeltTopologyChange(new[] { cell }, new[] { 8 }, new[] { edge }, new[] { edge }, new[] { state }) };
            var output = new BeltOutputResult(4, 9, 3, BeltDirection.Right, 32, true, item);
            var tick = new BeltTickMessagePack(new BeltTickDifference(124, changes, new[] { output }, changes, BeltTestState.Order(9, changes.Length, changes.Length)));
            var decoded = RoundTrip(tick).ToCore();
            Assert.AreEqual(124, decoded.Tick);
            Assert.AreEqual(9, decoded.Order.ServerTick);
            CollectionAssert.AreEqual(tick.BeforeSequenceIds, decoded.Order.BeforeSequenceIds);
            Assert.AreEqual(tick.SimulationSequenceId, decoded.Order.SimulationSequenceId);
            CollectionAssert.AreEqual(tick.AfterSequenceIds, decoded.Order.AfterSequenceIds);
            Assert.AreEqual(tick.CompletedSequenceId, decoded.Order.CompletedSequenceId);
            CollectionAssert.AreEqual(new[] { output }, decoded.Outputs);
            Assert.AreEqual(32, ((BeltSpeedChange)decoded.BeforeTick[0]).Speeds[0].Speed);
            Assert.AreEqual(item, ((BeltInputChange)decoded.BeforeTick[1]).Item);
            Assert.AreEqual(state, ((BeltCellItemsChange)decoded.BeforeTick[2]).Items[0]);
            var topology = (BeltTopologyChange)decoded.AfterTick[3];
            CollectionAssert.AreEqual(new[] { cell }, topology.ChangedCells);
            CollectionAssert.AreEqual(new[] { 8 }, topology.RemovedCells);
            CollectionAssert.AreEqual(new[] { edge }, topology.AddedConnections);
            CollectionAssert.AreEqual(new[] { edge }, topology.RemovedConnections);
            CollectionAssert.AreEqual(new[] { state }, topology.AddedItems);
        }
        internal static T RoundTrip<T>(T value) => MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));
    }
}
