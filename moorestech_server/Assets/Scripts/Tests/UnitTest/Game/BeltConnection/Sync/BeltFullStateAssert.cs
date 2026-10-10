using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Blocks.BeltConveyor.Transport;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltConnection.Sync
{
    // 全量同士・全量とサーバーの組・Core segment同士を項目ごとに比べる
    // Field-by-field comparison of full states, a full state against the server assembly, and Core segments against each other
    internal static class BeltFullStateAssert
    {
        internal static void AreEqual(BeltTransportFullState expected, BeltTransportFullState actual)
        {
            Assert.AreEqual(expected.Segments.Length, actual.Segments.Length, "segment count");
            for (var i = 0; i < expected.Segments.Length; i++)
            {
                var e = expected.Segments[i];
                var a = actual.Segments[i];
                AreEqualShapes(e.Shape, a.Shape, $"segment {i}");
                Assert.AreEqual(e.PriorityOrder, a.PriorityOrder, $"segment {i} priority order");
                Assert.AreEqual(e.Items.Length, a.Items.Length, $"segment {i} item count");
                for (var j = 0; j < e.Items.Length; j++) AreEqualItems(e.Items[j], a.Items[j], $"segment {i} item {j}");
                Assert.AreEqual(e.HasBufferItem, a.HasBufferItem, $"segment {i} has buffer item");
                if (e.HasBufferItem) AreEqualItems(e.BufferItem, a.BufferItem, $"segment {i} buffer item");
            }
        }

        // 全量がサーバーの構成とsegmentの中身をそのまま写しているか。切り出し側の取りこぼしを複製を介さずに検出する
        // Whether the full state mirrors the server's layouts and segment contents; catches capture omissions without going through the replica
        internal static void MirrorsAssembly(BeltTransportFullState full, BeltTransportAssembly assembly)
        {
            Assert.AreEqual(assembly.Layouts.Length, full.Segments.Length, "segment count against layouts");
            for (var i = 0; i < full.Segments.Length; i++)
            {
                var state = full.Segments[i];
                AreEqualLayout(assembly.Layouts[i], state.Shape, $"segment {i}");
                var segment = assembly.Segments[i];
                Assert.AreEqual(segment.PriorityOrder, state.PriorityOrder, $"segment {i} priority order against server");
                var items = segment.CaptureItems();
                Assert.AreEqual(items.Length, state.Items.Length, $"segment {i} item count against server");
                for (var j = 0; j < items.Length; j++)
                    AreEqualItems(new BeltItemSnapshot(items[j].Item, items[j].DistanceToExit), state.Items[j], $"segment {i} item {j} against server");

                // bufferはサーバーのbufferから直接読んで比べる
                // The buffer is compared by reading the server buffer directly
                var hasBuffer = false;
                var bufferItem = default(BeltItem);
                if (segment is BeltBufferedSegment buffered) hasBuffer = buffered.Buffer.TryGetItem(out bufferItem);
                Assert.AreEqual(hasBuffer, state.HasBufferItem, $"segment {i} buffer against server");
                if (hasBuffer) AreEqualItems(new BeltItemSnapshot(bufferItem, 0), state.BufferItem, $"segment {i} buffer item against server");
            }
        }

        // 複製のsegmentがサーバーと同じ型・容量・速度・個数で作られたか
        // Whether replica segments were created with the server's type, capacity, speed and count
        internal static void SameSegments(BeltConveyorSegment[] expected, BeltConveyorSegment[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, "segment count against replica");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Kind, actual[i].Kind, $"segment {i} kind");
                Assert.AreEqual(expected[i].GetType(), actual[i].GetType(), $"segment {i} type");
                Assert.AreEqual(expected[i].Capacity, actual[i].Capacity, $"segment {i} capacity");
                Assert.AreEqual(expected[i].Speed, actual[i].Speed, $"segment {i} speed");
                Assert.AreEqual(expected[i].Count, actual[i].Count, $"segment {i} running count");
                Assert.AreEqual(expected[i].PriorityOrder, actual[i].PriorityOrder, $"segment {i} priority order");
            }
        }

        private static void AreEqualShapes(BeltSegmentShape expected, BeltSegmentShape actual, string label)
        {
            Assert.AreEqual(expected.Kind, actual.Kind, $"{label} kind");
            Assert.AreEqual(expected.IsInternal, actual.IsInternal, $"{label} internal");
            Assert.AreEqual(expected.Speed, actual.Speed, $"{label} speed");
            Assert.AreEqual(expected.Forward, actual.Forward, $"{label} forward");
            Assert.AreEqual(expected.Capacity, actual.Capacity, $"{label} capacity");
            Assert.AreEqual(expected.Cells.Length, actual.Cells.Length, $"{label} cell count");
            for (var i = 0; i < expected.Cells.Length; i++)
            {
                Assert.AreEqual(expected.Cells[i].Position, actual.Cells[i].Position, $"{label} cell {i} position");
                Assert.AreEqual(expected.Cells[i].Forward, actual.Cells[i].Forward, $"{label} cell {i} forward");
            }
            AreEqualLinks(expected.Inputs, actual.Inputs, $"{label} inputs");
            AreEqualLinks(expected.Outputs, actual.Outputs, $"{label} outputs");
        }

        private static void AreEqualLinks(BeltLinkShape[] expected, BeltLinkShape[] actual, string label)
        {
            Assert.AreEqual(expected.Length, actual.Length, $"{label} count");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Direction, actual[i].Direction, $"{label} {i} direction");
                Assert.AreEqual(expected[i].EntryDirection, actual[i].EntryDirection, $"{label} {i} entry direction");
                Assert.AreEqual(expected[i].PartnerSegmentIndex, actual[i].PartnerSegmentIndex, $"{label} {i} partner");
            }
        }

        private static void AreEqualLayout(BeltSegmentLayout layout, BeltSegmentShape shape, string label)
        {
            Assert.AreEqual(layout.Kind, shape.Kind, $"{label} kind against layout");
            Assert.AreEqual(layout.IsInternal, shape.IsInternal, $"{label} internal against layout");
            Assert.AreEqual(layout.Speed, shape.Speed, $"{label} speed against layout");
            Assert.AreEqual(layout.Forward, shape.Forward, $"{label} forward against layout");
            Assert.AreEqual(layout.Capacity, shape.Capacity, $"{label} capacity against layout");
            Assert.AreEqual(layout.Cells.Length, shape.Cells.Length, $"{label} cell count against layout");
            for (var i = 0; i < layout.Cells.Length; i++)
            {
                Assert.AreEqual(layout.Cells[i].Position, shape.Cells[i].Position, $"{label} cell {i} position against layout");
                Assert.AreEqual(layout.Cells[i].Forward, shape.Cells[i].Forward, $"{label} cell {i} forward against layout");
            }
            AreEqualLayoutLinks(layout.Inputs, shape.Inputs, $"{label} inputs");
            AreEqualLayoutLinks(layout.Outputs, shape.Outputs, $"{label} outputs");
        }

        private static void AreEqualLayoutLinks(BeltSegmentLayoutLink[] expected, BeltLinkShape[] actual, string label)
        {
            Assert.AreEqual(expected.Length, actual.Length, $"{label} count against layout");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Direction, actual[i].Direction, $"{label} {i} direction against layout");
                Assert.AreEqual(expected[i].EntryDirection, actual[i].EntryDirection, $"{label} {i} entry direction against layout");
                Assert.AreEqual(expected[i].PartnerSegmentIndex, actual[i].PartnerSegmentIndex, $"{label} {i} partner against layout");
            }
        }

        private static void AreEqualItems(BeltItemSnapshot expected, BeltItemSnapshot actual, string label)
        {
            Assert.AreEqual(expected.ItemId, actual.ItemId, $"{label} item id");
            Assert.AreEqual(expected.ItemInstanceId, actual.ItemInstanceId, $"{label} instance id");
            Assert.AreEqual(expected.EntryDirection, actual.EntryDirection, $"{label} entry direction");
            Assert.AreEqual(expected.DistanceToExit, actual.DistanceToExit, $"{label} distance to exit");
        }
    }
}
