using Client.Tests.Ui.TestHost;
using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.Settings;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BugReport;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズ画面のトップ・子画面・切断表示・ロケール印・書きかけの保持を確かめる（L1。Web の pauseMenu.spec.ts・PauseMenuPanel.test.ts）
    /// Verifies the pause-menu top, sub-pages, disconnected line, locale marker and draft retention (L1; Web pauseMenu.spec.ts and PauseMenuPanel.test.ts)
    /// </summary>
    public class PauseMenuViewTest : ScreenTestHost
    {
        private FakePauseMenuModel _model;
        private RecordingPauseMenuCommands _commands;
        private BugReportDraft _draft;
        private PauseMenuView _view;

        [SetUp]
        public void MountPauseMenu()
        {
            _model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.Top));
            _commands = new RecordingPauseMenuCommands();
            _draft = new BugReportDraft();
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            _view = Mount(new PauseMenuView(_model.Current, settings.Current, _commands, new RecordingSettingsCommands(), Toasts, Toaster, _draft));
            _view.SetShown(true);
            simulate.FrameUpdate();
        }

        [Test]
        public void 隠している間はモデルの変化を描かず表示すると最新値で描き直す()
        {
            _view.SetShown(false);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, _view.resolvedStyle.display);
            _model.Set(FakePauseMenuModel.Page(PauseMenuPage.Settings));
            Assert.IsNotNull(_view.Q("pause-menu-save"), "隠している間に設定のページへ描き直した");

            _view.SetShown(true);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, _view.resolvedStyle.display);
            Assert.IsNotNull(_view.Q("language-select"), "表示しても最新のページで描き直さない");
            Assert.IsNull(_view.Q("pause-menu-save"));
        }

        /// <web-test file="src/features/pauseMenu/PauseMenuPanel.test.ts">トップは4ボタンだけを出し、言語選択と報告欄を出さない</web-test>
        /// <web-test file="src/features/pauseMenu/PauseMenuPanel.test.ts">設定ボタンはsettingsへのshow_pageを送る</web-test>
        [Test]
        public void トップは4ボタンだけを出し言語選択と報告欄を出さず各ボタンがコマンドを1回出す()
        {
            Assert.AreEqual(1, _view.Query("pause-menu").ToList().Count);
            Assert.AreEqual(4, _view.Query<Button>().ToList().Count);
            Assert.IsNull(_view.Q("language-select"));
            Assert.IsNull(_view.Q("bug-report-description"));

            simulate.Click(Find<Button>(_view, "pause-menu-save"));
            simulate.Click(Find<Button>(_view, "pause-menu-save-and-quit"));
            simulate.Click(Find<Button>(_view, "pause-menu-open-settings"));
            simulate.Click(Find<Button>(_view, "pause-menu-open-bug-report"));
            CollectionAssert.AreEqual(
                new[] { PauseMenuCommandKind.Save, PauseMenuCommandKind.SaveAndQuit, PauseMenuCommandKind.ShowPage, PauseMenuCommandKind.ShowPage },
                _commands.Issued.Select(record => record.Kind));
            Assert.AreEqual(PauseMenuPage.Settings, _commands.Issued[2].Page);
            Assert.AreEqual(PauseMenuPage.BugReport, _commands.Issued[3].Page);
        }

        [Test]
        public void ボタンの間隔はWebのStack_gap_mdの16pxで部品のmargin0に負けない()
        {
            var buttons = _view.Query<Button>().ToList();
            Assert.AreEqual(0f, buttons[0].resolvedStyle.marginTop);
            for (var i = 1; i < buttons.Count; i++) Assert.AreEqual(16f, buttons[i].resolvedStyle.marginTop, buttons[i].name);
        }

        [Test]
        public void トップの題とアンカーはWebと同じ綴り()
        {
            Assert.AreEqual("Pause Menu", _view.Q<Label>(className: "pause-menu__title").PlainText());
            Assert.IsTrue(Frame().ClassListContains(AnchorIds.PauseMenu));
            Assert.IsTrue(Find<Button>(_view, "pause-menu-save").ClassListContains(AnchorIds.PauseSave));
            Assert.IsTrue(Find<Button>(_view, "pause-menu-save-and-quit").ClassListContains(AnchorIds.PauseBack));
            Assert.IsTrue(Find<Button>(_view, "pause-menu-open-settings").ClassListContains(AnchorIds.PauseSettings));
            Assert.IsTrue(Find<Button>(_view, "pause-menu-open-bug-report").ClassListContains(AnchorIds.PauseBugReport));
        }

        /// <web-test file="e2e/tests/system/pauseMenu.spec.ts">pause_menu.currentの切断状態を表示する</web-test>
        [Test]
        public void 切断状態を赤字の1行で表示する()
        {
            var line = Find<Label>(_view, "pause-menu-disconnected");
            Assert.IsFalse(IsShown(line));

            _model.Set(new PauseMenuViewModel(PauseMenuPage.Top, true, FakePauseMenuModel.Capture(BugReportCaptureStatus.Ready), PlaytestDeliveryAllSent.Instance, PlaytestExportIdle.Instance));
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(line));
            Assert.AreEqual("Disconnected from server", line.PlainText());
            Assert.AreEqual(new Color(0xff / 255f, 0x87 / 255f, 0x87 / 255f), line.resolvedStyle.color);
        }

        [Test]
        public void 設定画面は言語選択と戻るボタンを出し戻るはtopへのshow_pageを送る()
        {
            ShowPage(PauseMenuPage.Settings);
            Assert.AreEqual("Settings", _view.Q<Label>(className: "pause-menu__title").PlainText());
            Assert.IsNotNull(_view.Q("language-select"));
            Assert.IsNull(_view.Q("pause-menu-save"));
            Assert.IsInstanceOf<GameScrollView>(Find<VisualElement>(_view, "pause-menu-body"));

            simulate.Click(Find<Button>(_view, "pause-menu-back"));
            Assert.AreEqual(PauseMenuCommandKind.ShowPage, _commands.Issued.Last().Kind);
            Assert.AreEqual(PauseMenuPage.Top, _commands.Issued.Last().Page);
            Assert.IsTrue(Find<Button>(_view, "pause-menu-back").ClassListContains(AnchorIds.PauseBackToTop));
            Assert.AreEqual(720f - 80f, Frame().worldBound.height, 0.5f);
            Assert.AreEqual(420f, Frame().worldBound.width, 0.5f);
            Assert.AreEqual(640f, Frame().worldBound.center.x, 0.5f);
        }

        /// <web-test file="src/features/pauseMenu/PauseMenuPanel.test.ts">バグ報告の書きかけはトップへ戻って再び開いても残る</web-test>
        [Test]
        public void バグ報告の書きかけはトップへ戻って再び開いても残る()
        {
            ShowPage(PauseMenuPage.BugReport);
            Assert.AreEqual("Bug report", _view.Q<Label>(className: "pause-menu__title").PlainText());
            Find<TextField>(_view, "bug-report-description").value = "keep me";
            Assert.AreEqual("keep me", _draft.Description);

            ShowPage(PauseMenuPage.Top);
            Assert.IsNull(_view.Q("bug-report-description"));
            ShowPage(PauseMenuPage.BugReport);
            Assert.AreEqual("keep me", Find<TextField>(_view, "bug-report-description").value);
        }

        [Test]
        public void ロケール印は言語の切り替えで名前を差し替える()
        {
            Assert.IsNotNull(_view.Q("pause-menu-locale-english"));
            Localize.TrySetChosenLanguage("japanese");
            Assert.IsNotNull(_view.Q("pause-menu-locale-japanese"));
            Assert.IsNull(_view.Q("pause-menu-locale-english"));
            Assert.AreEqual("ポーズメニュー", _view.Q<Label>(className: "pause-menu__title").PlainText());
        }

        [Test]
        public void トップの枠は中身の高さで中央に置かれる()
        {
            simulate.FrameUpdate();
            var frame = Frame();
            Assert.Less(frame.worldBound.height, 640f);
            Assert.AreEqual(360f, frame.worldBound.center.y, 1f);
            var saveAndQuit = Find<Button>(_view, "pause-menu-open-bug-report");
            Assert.LessOrEqual(saveAndQuit.worldBound.yMax, frame.worldBound.yMax + 0.5f);
        }

        private VisualElement Frame()
        {
            return Find<VisualElement>(_view, "pause-menu");
        }

        private void ShowPage(PauseMenuPage page)
        {
            // スクロールバーの出し入れで 2 回目のレイアウトが走るので、2 フレーム進めて落ち着かせる
            // Showing or hiding the scroller triggers a second layout, so advance two frames to settle
            _model.Set(FakePauseMenuModel.Page(page));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
