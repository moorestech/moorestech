using Client.Game.InGame.BugReport;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BugReport
{
    public class UnityLogRingTest
    {
        [Test]
        public void 上限を超えると先頭が消える()
        {
            var ring = new UnityLogRing();
            for (var i = 0; i < UnityLogRing.Capacity + 1; i++) ring.Add(LogType.Log, $"m{i}", "");
            var dump = ring.Dump();
            Assert.AreEqual(UnityLogRing.Capacity, dump.Count);
            Assert.AreEqual("m1", dump[0].Message);
            Assert.AreEqual($"m{UnityLogRing.Capacity}", dump[dump.Count - 1].Message);
        }

        [Test]
        public void エラー系だけスタックトレースを保持する()
        {
            var ring = new UnityLogRing();
            ring.Add(LogType.Log, "info", "trace-a");
            ring.Add(LogType.Error, "err", "trace-b");
            var dump = ring.Dump();
            Assert.AreEqual("", dump[0].StackTrace);
            Assert.AreEqual("trace-b", dump[1].StackTrace);
        }
    }
}
