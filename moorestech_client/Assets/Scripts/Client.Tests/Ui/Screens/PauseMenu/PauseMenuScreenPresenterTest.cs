using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using System.Linq;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.State;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの UI 状態への遷移で画面が出て、開くたびに書きかけが空へ戻ることを本物の Presenter と UiRoutingState で確かめる（L1。前例 InventoryScreenVisibilityTest・RecipeSelectionCloseResetTest）
    /// Verifies with the real presenter and UiRoutingState that entering the pause UI state shows the screen and every open clears the draft (L1; precedents InventoryScreenVisibilityTest and RecipeSelectionCloseResetTest)
    /// </summary>
    public class PauseMenuScreenPresenterTest : ScreenTestHost
    {
        private GameObject _host;
        private UIStateControl _control;
        private PauseMenuStateService _pauseMenuState;
        private RecordingPauseMenuCommands _commands;
        private BugReportDraft _draft;
        private PauseMenuScreenPresenter _presenter;

        [SetUp]
        public void MountPresenter()
        {
            // 行き来できるルータと本物のページ状態で組む
            // Build on a router and a real page state
            _host = new GameObject("pause-menu-presenter");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.PauseMenu }, out _control);
            _pauseMenuState = new PauseMenuStateService();
            _commands = new RecordingPauseMenuCommands();
            _draft = new BugReportDraft();
            var root = GameUiRootTestFactory.Create(_host, rootVisualElement);
            _presenter = new PauseMenuScreenPresenter(root, routing, PauseMenuModelTestFactory.Create(_pauseMenuState), new SettingsModel(), _commands, new RecordingSettingsCommands(), Toasts, Toaster, _draft, new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            _presenter.Initialize();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DestroyHost()
        {
            // Presenter はゲーム寿命で Dispose を持たない（S5-crosscut T13）。購読する源・ルータ・層はテストごとに作り直すので宿主を消すだけ
            // The presenter is game-lifetime with no Dispose (S5-crosscut T13); its sources, router and layers are rebuilt per test, so only the host goes
            Object.DestroyImmediate(_host);
        }

        /// <web-test file="e2e/tests/system/pauseMenu.spec.ts">PauseMenu遷移で表示しセーブとセーブして終了actionを送る</web-test>
        [Test]
        public void ポーズのUI状態へ遷移すると画面が出てセーブとセーブして終了のコマンドを出す()
        {
            var view = rootVisualElement.Q<PauseMenuView>();
            Assert.IsNotNull(view, "Presenter が View を層へ載せていない");
            Assert.IsFalse(IsShown(view), "ゲーム画面でポーズが出ている");

            // ポーズへ遷移すると画面が出る
            // Entering the pause shows the screen
            OpenPause();
            Assert.IsTrue(IsShown(view), "ポーズへ遷移しても画面が出ない");
            Assert.IsNotNull(view.Q("pause-menu"));

            // 各ボタンでコマンドが1回ずつ出る
            // Each button issues its command once
            simulate.Click(Find<Button>(view, "pause-menu-save"));
            simulate.Click(Find<Button>(view, "pause-menu-save-and-quit"));
            CollectionAssert.AreEqual(new[] { PauseMenuCommandKind.Save, PauseMenuCommandKind.SaveAndQuit }, _commands.Issued.Select(record => record.Kind));
        }

        /// <web-test file="src/features/pauseMenu/PauseMenuPanel.test.ts">パネルを外して付け直すと書きかけは空になる</web-test>
        [Test]
        public void ポーズを閉じて開き直すと書きかけは空になる()
        {
            // 1 回目のポーズでバグ報告に感想の書きかけを残す
            // Leave an impressions draft on the bug report during the first pause
            OpenPause();
            ShowPage(PauseMenuPage.BugReport);
            var view = rootVisualElement.Q<PauseMenuView>();
            simulate.Click(Find<Button>(view, "bug-report-kind-feedback"));
            Find<TextField>(view, "bug-report-description").value = "ベルトが止まる";
            Assert.AreEqual("ベルトが止まる", _draft.Description);

            // 開き直すと本文と種別が既定へ戻る
            // Reopening resets description and kind
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            Assert.IsFalse(IsShown(view), "閉じても画面が残る");
            OpenPause();
            ShowPage(PauseMenuPage.BugReport);
            Assert.AreEqual("", Find<TextField>(view, "bug-report-description").value);
            Assert.AreEqual("", _draft.Description);
            Assert.AreEqual(PlaytestReportKind.Bug, _draft.Kind);
            Assert.AreEqual("Send bug report", Find<FormButton>(view, "bug-report-send").Q<Label>().PlainText());
        }

        // 遷移後に開いたことを通知する
        // Announce the open after the transition
        private void OpenPause()
        {
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.PauseMenu);
            _pauseMenuState.OnEnter();
            simulate.FrameUpdate();
        }

        // ページ切替は本物の状態経由で届ける
        // Page changes reach the view via the real state
        private void ShowPage(PauseMenuPage page)
        {
            _pauseMenuState.ShowPage(page);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
