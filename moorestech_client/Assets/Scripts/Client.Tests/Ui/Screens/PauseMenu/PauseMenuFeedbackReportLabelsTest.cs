using Client.Tests.Ui.Screens.GrabOverlay;
using System.Linq;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.TestDoubles;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.Commands;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// 設定で言語を選んでから感想を送り、送信ボタンと完了のトーストがその言語の本番辞書の文言になることを確かめる（L1。Web の feedbackReportLabels.spec.ts）
    /// Verifies that after choosing a language in settings, the impressions send button and completion toast use that language's production text (L1; Web feedbackReportLabels.spec.ts)
    /// </summary>
    public class PauseMenuFeedbackReportLabelsTest : ScreenTestHost
    {
        private FakePauseMenuModel _model;
        private RecordingPauseMenuCommands _commands;
        private PauseMenuView _view;

        [SetUp]
        public void MountPauseMenu()
        {
            // 言語の選択は本物の SettingsCommands に通し、Localize の言語を実際に切り替える
            // Language choices go through the real SettingsCommands so Localize actually switches
            _model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.Top));
            _commands = new RecordingPauseMenuCommands();
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            _view = Mount(new PauseMenuView(_model.Current, settings.Current, _commands, new SettingsCommands(), Toasts, Toaster, new BugReportDraft(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f))));
            _view.SetShown(true);
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/system/feedbackReportLabels.spec.ts">感想送信のボタンと完了通知: ${label.locale}</web-test>
        [TestCase("japanese", "感想を送信", "感想をoutboxに書き出しました", "バグ報告を送信")]
        [TestCase("english", "Send impressions", "Impressions saved to outbox", "Send bug report")]
        [TestCase("german", "Eindrücke senden", "Eindrücke im Postausgang gespeichert", "Fehlerbericht senden")]
        [TestCase("korean", "감상 보내기", "감상이 보낼 편지함에 저장되었습니다", "버그 신고 보내기")]
        public void 設定で選んだ言語の本番辞書で感想の送信ボタンと完了通知を出す(string locale, string send, string sent, string bug)
        {
            // 設定で言語を選ぶとロケール印がその言語になる
            // Choosing a language in settings switches the locale marker to it
            ShowPage(PauseMenuPage.Settings);
            simulate.Click(Find<Button>(_view, $"language-select-option-{locale}"));
            Assert.IsNotNull(_view.Q($"pause-menu-locale-{locale}"), "言語の切り替えが画面に届かない");

            // トップ復帰でバグ文言に戻る
            // Back to top restores the bug label
            simulate.Click(Find<Button>(_view, "pause-menu-back"));
            ShowPage(PauseMenuPage.Top);
            simulate.Click(Find<Button>(_view, "pause-menu-open-bug-report"));
            ShowPage(PauseMenuPage.BugReport);
            Assert.AreEqual(bug, SendLabel());

            // 感想送信でボタンと通知が感想文言
            // Impressions send uses impressions labels
            simulate.Click(Find<Button>(_view, "bug-report-kind-feedback"));
            Assert.AreEqual(send, SendLabel());
            Find<TextField>(_view, "bug-report-description").value = "feedback label QA";
            simulate.Click(Find<FormButton>(_view, "bug-report-send"));
            _commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.AreEqual(sent, ShownToasts.Single().Text);
            Assert.AreEqual(GameUiToastVariant.Info, ShownToasts.Single().Variant);

            // 送った中身は本文と感想の種別
            // The submitted payload carries the description and the impressions kind
            var submit = _commands.Issued.Last();
            Assert.AreEqual(PauseMenuCommandKind.SubmitBugReport, submit.Kind);
            Assert.AreEqual("feedback label QA", submit.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, submit.ReportKind);

            // 開き直すと種別はバグへ、本文は空へ戻る
            // Reopening returns the kind to bug and the description to empty
            ShowPage(PauseMenuPage.Top);
            ShowPage(PauseMenuPage.BugReport);
            Assert.AreEqual(bug, SendLabel());
            Assert.AreEqual("", Find<TextField>(_view, "bug-report-description").value);
        }

        private string SendLabel()
        {
            return Find<FormButton>(_view, "bug-report-send").Q<Label>().PlainText();
        }

        // ページはホストが決めるので、押した後に偽モデルへ入れて描き直させる。スクロールバーの出し入れで 2 フレーム要る
        // The host decides the page, so push it into the fake model after the press; scroller toggling needs two frames
        private void ShowPage(PauseMenuPage page)
        {
            _model.Set(FakePauseMenuModel.Page(page));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
