using System.Linq;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.EntryDirection
{
    // 進入方向テスト用ヘルパー。公開APIだけを使う
    // Helpers for entry direction tests. Public API only
    public static class BeltEntryDirectionTestUtil
    {
        public static BeltItem MakeItemFrom(long serial, BeltEntryDirection entryDirection)
        {
            return MakeItem(serial).WithEntryDirection(entryDirection);
        }

        // (通し番号, 進入方向)を出口に近い順に検証する
        // Verify (serial, entry direction) pairs in exit order
        public static void AssertEntries(BeltConveyorSegment segment, params (long Serial, BeltEntryDirection Entry)[] expected)
        {
            var actual = segment.CaptureItems().Select(s => (Serial(s.Item), s.Item.EntryDirection)).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        public static void AssertBufferEntry(BeltBuffer buffer, long serial, BeltEntryDirection entryDirection)
        {
            Assert.IsTrue(buffer.TryGetItem(out var item));
            Assert.AreEqual(serial, Serial(item));
            Assert.AreEqual(entryDirection, item.EntryDirection);
        }
    }
}
