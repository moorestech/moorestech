using System;
using Client.Game.InGame.BugReport.Playtest;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class PlaytestReportKindTest
    {
        [Test]
        public void ポーズメニューから送れるのはバグと感想だけ()
        {
            Assert.IsTrue(PlaytestReportKindText.TryParseSubmittableFromPauseMenu("bug", out var bug));
            Assert.AreEqual(PlaytestReportKind.Bug, bug);
            Assert.IsTrue(PlaytestReportKindText.TryParseSubmittableFromPauseMenu("feedback", out var feedback));
            Assert.AreEqual(PlaytestReportKind.Feedback, feedback);

            Assert.IsFalse(PlaytestReportKindText.TryParseSubmittableFromPauseMenu("crash", out _));
            Assert.IsFalse(PlaytestReportKindText.TryParseSubmittableFromPauseMenu("bugs", out _));
            Assert.IsFalse(PlaytestReportKindText.TryParseSubmittableFromPauseMenu("", out _));
            Assert.IsFalse(PlaytestReportKindText.TryParseSubmittableFromPauseMenu(null, out _));
        }

        // 綴りは受け口・取り込み側との契約。書く側と読む側で片方だけ変わると取り込みが全件外れる
        // The spelling is the contract with the receiver and ingest; changing only one side would miss every ingest
        [Test]
        public void 全ての種別は契約値の綴りで往復する()
        {
            Assert.AreEqual("crash", PlaytestReportKindText.ToContractText(PlaytestReportKind.Crash));
            foreach (PlaytestReportKind kind in Enum.GetValues(typeof(PlaytestReportKind)))
            {
                Assert.IsTrue(PlaytestReportKindText.TryParse(PlaytestReportKindText.ToContractText(kind), out var restored), $"{kind} が読み戻せない");
                Assert.AreEqual(kind, restored);
            }
        }
    }
}
