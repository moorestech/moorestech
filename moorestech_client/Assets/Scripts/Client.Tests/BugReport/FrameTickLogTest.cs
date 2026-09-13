using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class FrameTickLogTest
    {
        // 区間と枚数まで残さないと、剪定で古い区間が消えたあとどの行が動画のどのフレームか辿れない
        // Without the segment and index, no row can be matched to a video frame once pruning drops the old segments
        [Test]
        public void 上限を超えると先頭が消えTSVは区間と枚数まで残す()
        {
            var log = new FrameTickLog();
            for (var i = 0; i < FrameTickLog.Capacity + 5; i++) log.Add(1000 + i, (ulong)(i / 2), 3, i);
            var rows = log.Dump();
            Assert.AreEqual(FrameTickLog.Capacity, rows.Count);
            Assert.AreEqual(1005, rows[0].UnixMs);
            Assert.AreEqual(5, rows[0].FrameIndex);
            var tsv = FrameTickLog.ToTsv(rows);
            StringAssert.StartsWith("unixMs\ttick\tsegment\tframe\n1005\t2\t3\t5\n", tsv);
        }
    }
}
