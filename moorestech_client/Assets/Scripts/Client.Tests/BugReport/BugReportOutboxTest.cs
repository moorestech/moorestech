using System;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Tests.BugReport.Bundle;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class BugReportOutboxTest
    {
        [Test]
        public void バンドルディレクトリ名は時刻と短いIDで一意になりREADYを置ける()
        {
            var dir = BugReportOutbox.CreateBundleDirectory(TestBugReportOutboxRoot.Directory, new DateTime(2026, 9, 11, 21, 5, 7, DateTimeKind.Utc), "0123abcd");
            StringAssert.StartsWith(TestBugReportOutboxRoot.Directory, dir);
            StringAssert.EndsWith("20260911_210507_0123abcd", dir);
            Assert.IsTrue(Directory.Exists(dir));
            BugReportOutbox.MarkReady(dir);
            Assert.IsTrue(File.Exists(Path.Combine(dir, BugReportOutbox.ReadyMarkerFileName)));
            Directory.Delete(dir, true);
        }
    }
}
