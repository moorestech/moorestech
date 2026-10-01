using System;
using Core.BeltTransport;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class BeltConveyorSpeedChangeTest : IBeltExternalReceiverFactory, IBeltItemDropObserver
    {
        public IBeltReceiver Create(BeltNetworkConnection connection, int stage) => throw new InvalidOperationException("No external endpoints.");
        public void OnDropped(BeltCellItemState item, string reason) => Assert.Fail(reason);
        [TestCase(4, 12)]
        [TestCase(0, 8)]
        [TestCase(8, 8)]
        public void SpeedChangesPreservePositionAndAdvanceWithNewIntegerSpeedTest(int initialSpeed, int nextSpeed)
        {
            var network = new BeltTransportNetwork(this, this);
            var item = new BeltItem(new Guid("00000000-0000-0000-0000-000000000001"), 1);
            var cell = new BeltNetworkCell(1, 0, 0, 0, initialSpeed, "fixed:1", BeltDirection.Front, new BeltCellSurfaceProfile(0.1f, 1.1f));
            var state = new BeltCellItemState(1, 96, BeltDirection.Back, 0, item, false);
            network.Restore(new BeltNetworkSnapshot(new[] { cell }, Array.Empty<BeltNetworkConnection>(), new[] { state }, Array.Empty<BeltCellPriority>()));

            // 実運用の速度変更で占有位置・識別子・表示面を保つ。
            // Preserve occupied progress, identity, and surfaces through the runtime speed-change path.
            network.SetSpeeds(new[] { new BeltCellSpeed(1, nextSpeed) });
            CollectionAssert.AreEqual(new[] { state }, network.CaptureItems());
            Assert.AreEqual(cell.Surface, network.Capture().Cells[0].Surface);
            network.Tick();
            Assert.AreEqual(96 + nextSpeed, network.CaptureItems()[0].Progress);
            Assert.AreEqual(item, network.CaptureItems()[0].Item);
        }
    }
}
