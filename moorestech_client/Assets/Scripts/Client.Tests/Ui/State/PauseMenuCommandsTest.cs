using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.State.Commands;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State
{
    public class PauseMenuCommandsTest
    {
        [Test]
        public void 空白だけの本文はEmptyDescriptionで拒否しトップへ戻らない()
        {
            var service = new PauseMenuStateService();
            service.ShowPage(PauseMenuPage.BugReport);
            var commands = PauseMenuCommandsTestFactory.Create(service);

            LogAssert.Expect(LogType.Warning, new Regex("バグ報告の説明文が空"));
            var result = commands.SubmitBugReport("  \n\t ", PlaytestReportKind.Bug).GetAwaiter().GetResult();

            var rejected = AssertRejected(result, UiCommandRejectReason.EmptyDescription);
            Assert.AreEqual("empty_description", rejected.ToWireCode());
            Assert.AreEqual(PauseMenuPage.BugReport, service.CurrentPage.Value);
        }

        [Test]
        public void クラッシュ種別はUnsubmittableReportKindで拒否する()
        {
            var service = new PauseMenuStateService();
            service.ShowPage(PauseMenuPage.BugReport);
            var commands = PauseMenuCommandsTestFactory.Create(service);

            LogAssert.Expect(LogType.Warning, new Regex("ポーズから送れない"));
            var result = commands.SubmitBugReport("説明", PlaytestReportKind.Crash).GetAwaiter().GetResult();

            var rejected = AssertRejected(result, UiCommandRejectReason.UnsubmittableReportKind);
            Assert.AreEqual("invalid_kind", rejected.ToWireCode());
            Assert.AreEqual(PauseMenuPage.BugReport, service.CurrentPage.Value);
        }

        [Test]
        public void 確保が無い送信はCaptureRefusedで送信手続きの失敗コードを補足に持つ()
        {
            // 確保なしのセッションは送信手続きが拒否する。拒否の失敗コードがそのまま Web の契約値になる
            // A session with no capture is refused by the submitter; its failure code becomes the Web contract value as is
            var service = new PauseMenuStateService();
            service.ShowPage(PauseMenuPage.BugReport);
            var commands = PauseMenuCommandsTestFactory.Create(service);

            LogAssert.Expect(LogType.Warning, new Regex("確保セッションに拒否"));
            var result = commands.SubmitBugReport("説明", PlaytestReportKind.Feedback).GetAwaiter().GetResult();

            var rejected = AssertRejected(result, UiCommandRejectReason.CaptureRefused);
            // 送信手続きの失敗コードはそのまま返る
            // Submitter failure code is returned as is
            Assert.IsNotEmpty(rejected.ToWireCode());
            Assert.AreNotEqual(UiCommandRejectReasonTraits.ToWireCode(UiCommandRejectReason.CaptureRefused), rejected.ToWireCode());
            Assert.AreEqual(PauseMenuPage.BugReport, service.CurrentPage.Value);
        }

        [TestCase(PauseMenuPage.Settings)]
        [TestCase(PauseMenuPage.BugReport)]
        public void ShowPageで現在の画面が変わる(PauseMenuPage page)
        {
            var service = new PauseMenuStateService();
            var commands = PauseMenuCommandsTestFactory.Create(service);

            commands.ShowPage(page);

            Assert.AreEqual(page, service.CurrentPage.Value);
        }

        // 書き出し失敗だけを BundleWriteFailed に写し、確保セッションの拒否コードは CaptureRefused に畳む
        // Only a write failure maps to BundleWriteFailed; the capture session's refusal codes fold into CaptureRefused
        [TestCase(BugReportSubmitResult.BundleWriteFailed, UiCommandRejectReason.BundleWriteFailed)]
        [TestCase("no_capture_session", UiCommandRejectReason.CaptureRefused)]
        [TestCase("capture_pending", UiCommandRejectReason.CaptureRefused)]
        [TestCase("submit_in_flight", UiCommandRejectReason.CaptureRefused)]
        public void 送信手続きの失敗コードを拒否理由へ写す(string failureCode, UiCommandRejectReason expected)
        {
            Assert.AreEqual(expected, PauseMenuCommands.FromSubmitFailureCode(failureCode));
        }

        [Test]
        public void 書き出し失敗の既定の失敗コードは送信手続きの定数と一致する()
        {
            // 理由表は Client.Game を参照しないので綴りを写している。送信手続き側の改名で黙ってずれないよう見張る
            // The reason table does not reference Client.Game and copies the spelling; guard it against a silent drift on the submitter side
            Assert.AreEqual(BugReportSubmitResult.BundleWriteFailed, UiCommandRejectReasonTraits.ToWireCode(UiCommandRejectReason.BundleWriteFailed));
        }

        private static UiCommandRejected AssertRejected(UiCommandResult result, UiCommandRejectReason expected)
        {
            Assert.IsInstanceOf<UiCommandRejected>(result);
            var rejected = (UiCommandRejected)result;
            Assert.AreEqual(expected, rejected.Reason);
            return rejected;
        }
    }
}
