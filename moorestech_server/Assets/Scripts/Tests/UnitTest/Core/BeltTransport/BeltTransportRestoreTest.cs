using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltTransportTestFactory;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltTransportRestoreTest
    {
        private static readonly (int Capacity, int Speed)[] Configuration = { (1, 64), (2, 64), (3, 64), (4, 32) };
        [Test]
        public void CapturedSnapshotReplaysExactlyAfterRestoreTest()
        {
            var network = CreateNetwork(Configuration, new[] { (0, 1, BeltDirection.Front),
                (1, 2, BeltDirection.Left), (1, 3, BeltDirection.Right),
                (2, 0, BeltDirection.Right), (3, 0, BeltDirection.Left) });
            var original = new[] { Segment(network, 0), Segment(network, 1), Segment(network, 2), Segment(network, 3) };
            original[1].Buffer.RestoreItem(Item(1));
            original[2].RestoreItems(new[] { new BeltItemState(Item(2), 0), new BeltItemState(Item(3), 300) });
            original[3].RestoreItems(new[] { new BeltItemState(Item(4), 128), new BeltItemState(Item(5), 600) });
            for (int tick = 0; tick < 17; tick++) network.Tick();

            // 実snapshotから速度・順序・列・バッファを復元する。
            // Restore speed, priorities, queues and buffers from the actual network snapshot.
            var loaded = RestoreNetwork(network.Capture());
            var restored = new[] { Segment(loaded, 0), Segment(loaded, 1), Segment(loaded, 2), Segment(loaded, 3) };

            // ラップと優先順変更を跨いでも同じ状態へ進む。
            // Verify identical evolution across ring wraps and priority rotations.
            for (int tick = 0; tick < 200; tick++)
            {
                network.Tick();
                loaded.Tick();
                for (int i = 0; i < original.Length; i++)
                {
                    CollectionAssert.AreEqual(original[i].CaptureItems(), restored[i].CaptureItems());
                    Assert.AreEqual(original[i].PriorityOrder, restored[i].PriorityOrder);
                    if (original[i].Buffer == null) continue;
                    Assert.AreEqual(original[i].Buffer.TryGetItem(out var first), restored[i].Buffer.TryGetItem(out var second));
                    Assert.AreEqual(first, second);
                }
            }
        }

        [Test]
        public void NormalCycleResultDoesNotDependOnUpdateOrderTest()
        {
            var forward = CreateCycle();
            var reverse = CreateCycle();
            var first = new BeltSimulation(forward);
            var second = new BeltSimulation(new[] { reverse[2], reverse[1], reverse[0] });

            // 空き記録と反映を分離すると速度境界の処理順に依存しない。
            // Separating offers from application removes boundary update-order dependence.
            for (int tick = 0; tick < 100; tick++)
            {
                first.Tick();
                second.Tick();
                var total = 0;
                for (int i = 0; i < forward.Length; i++)
                {
                    CollectionAssert.AreEqual(forward[i].CaptureItems(), reverse[i].CaptureItems());
                    total += forward[i].CaptureItems().Length;
                }
                Assert.AreEqual(3, total);
            }

            #region Internal
            BeltConveyorSegment[] CreateCycle()
            {
                var network = CreateNetwork(new[] { (2, 32), (2, 64), (2, 128) },
                    new[] { (0, 1, BeltDirection.Front), (1, 2, BeltDirection.Front), (2, 0, BeltDirection.Front) });
                var result = new[] { Segment(network, 0), Segment(network, 1), Segment(network, 2) };
                for (int i = 0; i < result.Length; i++)
                {
                    result[i].RestoreItems(new[] { new BeltItemState(Item(i + 1), i * 64) });
                }
                return result;
            }
            #endregion
        }

    }
}
