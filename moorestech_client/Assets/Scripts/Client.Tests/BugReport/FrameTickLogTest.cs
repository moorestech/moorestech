using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class FrameTickLogTest
    {
        [Test]
        public void 上限を超えると先頭が消えTSVは2列()
        {
            var log = new FrameTickLog();
            for (var i = 0; i < FrameTickLog.Capacity + 5; i++) log.Add(1000 + i, (ulong)(i / 2));
            var rows = log.Dump();
            Assert.AreEqual(FrameTickLog.Capacity, rows.Count);
            Assert.AreEqual(1005, rows[0].unixMs);
            var tsv = FrameTickLog.ToTsv(rows);
            StringAssert.StartsWith("unixMs\ttick\n1005\t2\n", tsv);
        }
    }
}
