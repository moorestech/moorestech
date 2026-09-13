using System;
using System.IO;
using System.Linq;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot.Replay;
using Server.Protocol.PacketResponse;

namespace Tests.CombinedTest.Server.Replay
{
    public class PacketLogJsonDumperTest
    {
        [Test]
        public void パケットログをtickとタグ付きのJSON行へ書き出す()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-dump-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Append(5, MessagePackSerializer.Serialize(new SaveProtocol.SaveProtocolMessagePack()));
            log.Append(7, MessagePackSerializer.Serialize(BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest()));
            log.Flush();

            var output = Path.Combine(dir, "packets.jsonl");
            var count = PacketLogJsonDumper.Dump(log.SegmentFilePaths(), output);
            Assert.AreEqual(2, count);
            var lines = File.ReadAllLines(output).Select(JObject.Parse).ToList();
            Assert.AreEqual(5UL, (ulong)lines[0]["tick"]);
            Assert.AreEqual(SaveProtocol.ProtocolTag, (string)lines[0]["tag"]);
            Assert.AreEqual(BugReportCaptureProtocol.ProtocolTag, (string)lines[1]["tag"]);
            Assert.IsTrue(lines[1]["json"].ToString().Contains("va:bugReportCapture"));

            log.Stop();
            Directory.Delete(dir, true);
        }
    }
}
