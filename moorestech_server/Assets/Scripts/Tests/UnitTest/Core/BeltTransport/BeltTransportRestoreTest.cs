using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltTransportTestFactory;

namespace Tests.UnitTest.Core.BeltTransport
{
    public class BeltTransportRestoreTest
    {
        private static readonly (int Capacity, int Speed, BeltSegmentKind Kind)[] Configuration = {
            (1, 64, BeltSegmentKind.Merge), (2, 64, BeltSegmentKind.Branch),
            (3, 64, BeltSegmentKind.Normal), (4, 32, BeltSegmentKind.Normal) };
        [Test]
        public void CapturedSnapshotReplaysExactlyAfterRestoreTest()
        {
            var original = BuildNetwork();
            var simulation = new BeltSimulation(original);
            for (int tick = 0; tick < 17; tick++) simulation.Tick();
            var restored = new BeltConveyorSegment[original.Length];

            // 速度・順序・列・バッファを復元。
            // Restore speed, priority, queue and buffer state.
            for (int i = 0; i < original.Length; i++)
            {
                var source = original[i];
                restored[i] = new BeltConveyorSegment(Configuration[i].Capacity, Configuration[i].Speed, Configuration[i].Kind, source.PriorityOrder, BeltDirection.Front);
                restored[i].RestoreItems(source.CaptureItems());
                if (source.Buffer != null && source.Buffer.TryGetItem(out var item)) restored[i].Buffer.RestoreItem(item);
            }
            Connect(restored);
            var restoredSimulation = new BeltSimulation(restored);

            // ラップと優先順変更を跨いでも同じ状態へ進む。
            // Verify identical evolution across ring wraps and priority rotations.
            for (int tick = 0; tick < 200; tick++)
            {
                simulation.Tick();
                restoredSimulation.Tick();
                for (int i = 0; i < original.Length; i++)
                {
                    CollectionAssert.AreEqual(original[i].CaptureItems(), restored[i].CaptureItems());
                    Assert.AreEqual(original[i].PriorityOrder, restored[i].PriorityOrder);
                    if (original[i].Buffer == null) continue;
                    Assert.AreEqual(original[i].Buffer.TryGetItem(out var first), restored[i].Buffer.TryGetItem(out var second));
                    Assert.AreEqual(first, second);
                }
            }

            #region Internal
            BeltConveyorSegment[] BuildNetwork()
            {
                var result = new BeltConveyorSegment[Configuration.Length];
                for (int i = 0; i < result.Length; i++)
                    result[i] = Create(Configuration[i].Capacity, Configuration[i].Speed, Configuration[i].Kind);
                Connect(result);
                result[1].Buffer.RestoreItem(Item(1));
                result[2].RestoreItems(new[] { new BeltItemState(Item(2), 0), new BeltItemState(Item(3), 300) });
                result[3].RestoreItems(new[] { new BeltItemState(Item(4), 128), new BeltItemState(Item(5), 600) });
                return result;
            }
            #endregion
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
                var result = new[] { Create(2, 32, BeltSegmentKind.Normal), Create(2, 64, BeltSegmentKind.Normal), Create(2, 128, BeltSegmentKind.Normal) };
                for (int i = 0; i < result.Length; i++)
                {
                    result[i].ConnectTo(result[(i + 1) % result.Length], BeltDirection.Front);
                    result[i].RestoreItems(new[] { new BeltItemState(Item(i + 1), i * 64) });
                }
                return result;
            }
            #endregion
        }

        private static void Connect(BeltConveyorSegment[] network)
        {
            network[0].Buffer.ConnectTo(network[1], BeltDirection.Front);
            network[1].Buffer.ConnectTo(network[2], BeltDirection.Left);
            network[1].Buffer.ConnectTo(network[3], BeltDirection.Right);
            network[2].ConnectTo(network[0], BeltDirection.Right);
            network[3].ConnectTo(network[0], BeltDirection.Left);
        }

    }
}
