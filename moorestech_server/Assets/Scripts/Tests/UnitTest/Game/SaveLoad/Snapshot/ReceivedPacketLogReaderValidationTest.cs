using System;
using System.IO;
using System.Linq;
using Game.SaveLoad.Snapshot;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad.Snapshot
{
    public class ReceivedPacketLogReaderValidationTest
    {
        [Test]
        public void 負の送り手IDを持つパケットレコードを拒否する()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-invalid-packet-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1, Array.Empty<int>());
            log.Append(2, -7, new byte[] { 1 });
            log.Stop();

            // 壊れた送り手を再生の接続集合へ登録させない
            // Never register a corrupt sender in the replay connection set
            var exception = Assert.Throws<InvalidDataException>(() => ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths()));
            StringAssert.Contains("送り手IDが不正", exception.Message);
            Directory.Delete(dir, true);
        }

        [Test]
        public void 未紐づけ切断をnullの送り手として読み戻せる()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-unbound-disconnect-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1, Array.Empty<int>());
            log.AppendDisconnect(2, null);
            log.Stop();

            var record = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths()).Single();
            Assert.AreEqual(ReceivedPacketRecordKind.Disconnect, record.Kind);
            Assert.IsNull(record.PlayerId);
            Directory.Delete(dir, true);
        }
    }
}
