using System;
using System.IO;
using System.Linq;
using Game.SaveLoad.Snapshot;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ReceivedPacketLogTest
    {
        [Test]
        public void 追記したレコードをtick付きで読み戻せる()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Append(1, new byte[] { 1, 2, 3 });
            log.Append(3, new byte[] { 9 });
            log.Rotate(4);
            log.Append(4, new byte[] { 4, 4 });
            log.Flush();

            var records = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths());
            Assert.AreEqual(3, records.Count);
            Assert.AreEqual(1UL, records[0].Tick);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, records[0].Payload);
            Assert.AreEqual(3UL, records[1].Tick);
            Assert.AreEqual(4UL, records[2].Tick);
            Directory.Delete(dir, true);
        }

        [Test]
        public void 最古スナップショット以前の区間だけ削除される()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Rotate(11);
            log.Rotate(21);
            log.Rotate(31);
            log.DeleteSegmentsBefore(20);

            var names = log.SegmentFilePaths().Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "packets_21.bin", "packets_31.bin" }, names);
            Directory.Delete(dir, true);
        }

        [Test]
        public void 開始前のAppendは無視される()
        {
            var log = new ReceivedPacketLog();
            log.Append(1, new byte[] { 1 });
            log.Append(2, new byte[] { 1 });
            Assert.IsFalse(log.IsActive);
        }
    }
}
