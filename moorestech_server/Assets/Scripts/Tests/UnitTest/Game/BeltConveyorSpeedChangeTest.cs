using System;
using Core.BeltTransport;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class BeltConveyorSpeedChangeTest
    {
        [TestCase(4, 12)]
        [TestCase(0, 8)]
        [TestCase(8, 8)]
        public void SpeedChangesPreservePositionAndAdvanceWithNewIntegerSpeedTest(int initialSpeed, int nextSpeed)
        {
            var segment = new BeltConveyorSegment(1, initialSpeed, BeltSegmentKind.Normal, -1, BeltDirection.Front);
            var item = new BeltItem(new Guid("00000000-0000-0000-0000-000000000001"), 1);
            segment.RestoreItems(new[] { new BeltItemState(item, 160) });
            var simulation = new BeltSimulation(new[] { segment });

            // 停止からの復帰でも整数位置を再換算しない。
            // Preserve the integer position even when recovering from a stop.
            segment.SetSpeed(nextSpeed);
            Assert.AreEqual(160, segment.CaptureItems()[0].DistanceToExit);
            simulation.Tick();
            Assert.AreEqual(160 - nextSpeed, segment.CaptureItems()[0].DistanceToExit);
            Assert.AreEqual(item.Guid, segment.CaptureItems()[0].Item.Guid);
        }
    }
}
