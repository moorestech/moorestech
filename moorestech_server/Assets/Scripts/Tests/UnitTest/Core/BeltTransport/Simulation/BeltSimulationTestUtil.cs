using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation
{
    // BeltSimulationのテスト用ヘルパー。公開APIだけを使い、リフレクションは使わない
    // Helpers for BeltSimulation tests. Public API only, no reflection
    public static class BeltSimulationTestUtil
    {
        public static BeltMergeSegment CreateMerge(int speed, BeltDirection forwardDirection)
        {
            return new BeltMergeSegment(speed, BeltPriority.InitializeFromDirection, forwardDirection);
        }

        public static BeltBranchSegment CreateBranch(int capacity, int speed, int priorityOrder, BeltDirection forwardDirection)
        {
            return new BeltBranchSegment(capacity, speed, priorityOrder, forwardDirection);
        }

        public static long Serial(in BeltItem item)
        {
            return item.ItemInstanceId.AsPrimitive();
        }

        // (通し番号, 出口までの距離)を出口に近い順に復元する
        // Restore (serial, distance to exit) pairs in exit order
        public static void Restore(BeltConveyorSegment segment, params (long Serial, int Distance)[] items)
        {
            segment.RestoreItems(items.Select(i => new BeltItemState(MakeItem(i.Serial), i.Distance)).ToArray());
        }

        // firstSerialから連番のcount個を出口から密着して並べる
        // Place count consecutive serials from firstSerial packed from the exit
        public static void RestorePacked(BeltConveyorSegment segment, long firstSerial, int count)
        {
            var items = new (long, int)[count];
            for (var i = 0; i < count; i++) items[i] = (firstSerial + i, i * W);
            Restore(segment, items);
        }

        public static void AssertItems(BeltConveyorSegment segment, params (long Serial, int Distance)[] expected)
        {
            var actual = segment.CaptureItems().Select(s => (Serial(s.Item), s.DistanceToExit)).ToArray();
            CollectionAssert.AreEqual(expected, actual, $"expected [{Format(expected)}] but was [{Format(actual)}]");
        }

        public static long[] Serials(IEnumerable<BeltItem> items)
        {
            return items.Select(i => Serial(i)).ToArray();
        }

        // bufferの保持アイテムの通し番号。空なら-1
        // Serial of the buffer's held item; -1 when empty
        public static long BufferSerial(BeltBuffer buffer)
        {
            return buffer.TryGetItem(out var item) ? Serial(item) : -1;
        }

        public static void TickTimes(BeltSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks; i++) simulation.Tick();
        }

        private static string Format(IEnumerable<(long Serial, int Distance)> items)
        {
            return string.Join(", ", items.Select(i => $"#{i.Serial}@{i.Distance}"));
        }
    }
}
