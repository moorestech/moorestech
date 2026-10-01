using System;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltTransportTestFactory;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltTransportRestoreTest
    {
        [Test]
        public void CapturedSnapshotReplaysExactlyAfterRestoreTest()
        {
            var network = CreateNetwork(new[] { (1, 64), (2, 64), (3, 64), (4, 32) },
                new[] { (0, 1, BeltDirection.Front), (1, 2, BeltDirection.Left), (1, 3, BeltDirection.Right),
                    (2, 0, BeltDirection.Right), (3, 0, BeltDirection.Left) });
            SetItems(network, new[] { State(102, 256, 1, true), State(203, 256, 2, false),
                State(202, 212, 3, false), State(304, 128, 4, false), State(302, 168, 5, false) });
            for (int tick = 0; tick < 17; tick++) network.Tick();
            var loaded = RestoreNetwork(network.Capture());
            // ラップと優先順変更を跨いでも同じ状態へ進む。
            // Verify identical evolution across ring wraps and priority rotations.
            for (int tick = 0; tick < 200; tick++)
            {
                network.Tick();
                loaded.Tick();
                CollectionAssert.AreEqual(network.CaptureItems(), loaded.CaptureItems());
                CollectionAssert.AreEqual(network.Capture().Priorities, loaded.Capture().Priorities);
            }
        }

        [Test]
        public void NormalCycleResultDoesNotDependOnRegistrationOrderTest()
        {
            var first = CreateNetwork(new[] { (2, 32), (2, 64), (2, 128) },
                new[] { (0, 1, BeltDirection.Front), (1, 2, BeltDirection.Front), (2, 0, BeltDirection.Front) });
            SetItems(first, new[] { State(2, 256, 1, false), State(102, 192, 2, false), State(202, 128, 3, false) });
            var reversed = first.Capture();
            Array.Reverse(reversed.Cells);
            Array.Reverse(reversed.Connections);
            var second = RestoreNetwork(reversed);
            // 登録順の異なるグラフも同じtick処理を通す。
            // Run the actual tick pipeline with different graph registration orders.
            for (int tick = 0; tick < 100; tick++)
            {
                first.Tick();
                second.Tick();
                CollectionAssert.AreEqual(first.CaptureItems(), second.CaptureItems());
                Assert.AreEqual(3, first.CaptureItems().Length);
            }
        }
    }
}
