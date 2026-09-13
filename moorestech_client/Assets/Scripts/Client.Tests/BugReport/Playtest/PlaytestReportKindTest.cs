using Client.Game.InGame.BugReport.Playtest;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class PlaytestReportKindTest
    {
        [Test]
        public void ポーズメニューから送れるのはバグと感想だけ()
        {
            Assert.IsTrue(PlaytestReportKind.IsSubmittableFromPauseMenu(PlaytestReportKind.Bug));
            Assert.IsTrue(PlaytestReportKind.IsSubmittableFromPauseMenu(PlaytestReportKind.Feedback));
            Assert.IsFalse(PlaytestReportKind.IsSubmittableFromPauseMenu(PlaytestReportKind.Crash));
            Assert.IsFalse(PlaytestReportKind.IsSubmittableFromPauseMenu("bugs"));
            Assert.IsFalse(PlaytestReportKind.IsSubmittableFromPauseMenu(""));
            Assert.IsFalse(PlaytestReportKind.IsSubmittableFromPauseMenu(null));
        }

        [Test]
        public void クラッシュは既知の種別として扱う()
        {
            Assert.IsTrue(PlaytestReportKind.IsKnown(PlaytestReportKind.Crash));
            Assert.IsFalse(PlaytestReportKind.IsKnown("unknown"));
        }
    }
}
