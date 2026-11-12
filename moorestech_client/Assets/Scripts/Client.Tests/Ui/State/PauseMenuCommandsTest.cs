using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Localization;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.State.Commands;
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
            Assert.IsNotEmpty(rejected.WireCodeOverride);
            Assert.AreEqual(rejected.WireCodeOverride, rejected.ToWireCode());
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

        [Test]
        public void 良性の拒否理由は宣言した一覧と一致する()
        {
            // 選択済みの再送・持ち物の取りこぼし・特殊族の古い操作・空の掴みのフィルタ設定・閉じた後の選択・消えた BP の削除・受理外のホットバー選択・古いモーダル応答は利用者に知らせない（ログだけ）
            // Resends, inventory misses, special-family stale operations, filter sets with an empty grab, selections after close, deletes of vanished blueprints, non-accepting hotbar taps and stale modal replies are not told to players (log only)
            var benign = new[] { UiCommandRejectReason.LanguageAlreadySelected, UiCommandRejectReason.ItemSourceSlotEmpty, UiCommandRejectReason.ItemSourceCountShort, UiCommandRejectReason.SplitGrabNotEmpty, UiCommandRejectReason.SpecialBlockCommandStale, UiCommandRejectReason.FilterSplitterGrabEmpty, UiCommandRejectReason.BuildMenuNotOpen, UiCommandRejectReason.UnknownBuildMenuEntry, UiCommandRejectReason.BlueprintDeleteNotFound, UiCommandRejectReason.HotbarSelectNotAccepted, UiCommandRejectReason.ModalResponseStale };
            foreach (UiCommandRejectReason reason in System.Enum.GetValues(typeof(UiCommandRejectReason)))
            {
                Assert.AreEqual(0 <= System.Array.IndexOf(benign, reason), UiCommandRejectReasonTraits.IsBenign(reason), reason.ToString());
            }
        }

        // 書き出し失敗だけを BundleWriteFailed に写し、確保セッションの拒否コードは CaptureRefused に畳む
        // Only a write failure maps to BundleWriteFailed; the capture session's refusal codes fold into CaptureRefused
        [TestCase(BugReportSubmitResult.BundleWriteFailed, UiCommandRejectReason.BundleWriteFailed)]
        [TestCase("no_capture_session", UiCommandRejectReason.CaptureRefused)]
        [TestCase("capture_pending", UiCommandRejectReason.CaptureRefused)]
        [TestCase("submit_in_flight", UiCommandRejectReason.CaptureRefused)]
        public void 送信手続きの失敗コードを拒否理由へ写す(string failureCode, UiCommandRejectReason expected)
        {
            Assert.AreEqual(expected, UiCommandRejectReasonTraits.FromSubmitFailureCode(failureCode));
        }

        [Test]
        public void どの拒否理由もトースト用の翻訳文を持つ()
        {
            // 単独実行でも辞書が引けるように初期化する
            // Initialize the dictionary so the test also passes when run alone
            Localize.Initialize();
            foreach (UiCommandRejectReason reason in System.Enum.GetValues(typeof(UiCommandRejectReason)))
            {
                var text = Localize.Get(UiCommandRejectReasonTraits.ToLocalizationKey(reason));
                Assert.IsNotEmpty(text, reason.ToString());
                StringAssert.DoesNotContain("_", text, $"{reason} の文言が失敗コードのまま");
                StringAssert.DoesNotStartWith("[!", text, $"{reason} の翻訳キーが辞書に無い");
            }
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
