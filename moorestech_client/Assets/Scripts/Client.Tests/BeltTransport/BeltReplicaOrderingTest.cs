using Core.BeltTransport;
using Client.Game.InGame.BeltTransport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltReplicaOrderingTest
    {
        [Test]
        public void MissingTickWaitsAndBufferedTicksAdvanceExactlyOnceTest()
        {
            var replica = new BeltClientReplica(new BeltCommittedSnapshot(10, BeltTestState.Snapshot(1, 32, true)));
            replica.Receive(BeltTestState.Tick(10));
            LogAssert.Expect(LogType.Warning, "Belt transport waiting for tick 11; received 12.");
            replica.Receive(BeltTestState.Tick(12));
            Assert.AreEqual(10, replica.Tick);
            Assert.AreEqual(1, replica.Snapshot.Items[0].Progress);
            replica.Receive(BeltTestState.Tick(11));
            Assert.AreEqual(12, replica.Tick);
            Assert.AreEqual(65, replica.Snapshot.Items[0].Progress);
            replica.Receive(BeltTestState.Tick(11));
            replica.Receive(BeltTestState.Tick(12));
            Assert.AreEqual(65, replica.Snapshot.Items[0].Progress);
        }
        [TestCase(BeltDirection.Front, 0, 128, 0, 0.5f)]
        [TestCase(BeltDirection.Left, 1, 64, -0.75f, 0)]
        [TestCase(BeltDirection.Right, -1, 192, 0.25f, 0)]
        [TestCase(BeltDirection.Back, 0, 256, 0, 0)]
        public void CellCenterEntryAndSlopePositionTest(BeltDirection entry, int height, int progress, float x, float z)
        {
            var state = BeltTestState.Snapshot(progress, 0, true);
            var item = new BeltCellItemState(1, progress, entry, height, state.Items[0].Item, false);
            var position = BeltItemPosition.Calculate(state.Cells[0], item);
            Assert.AreEqual(new Vector3(-1.5f + x, 3.35f + height * (1f - progress / 256f), 4.5f + z), position);
        }
    }
}
