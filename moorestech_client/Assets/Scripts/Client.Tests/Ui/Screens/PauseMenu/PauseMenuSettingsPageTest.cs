using Client.Tests.Ui.Screens.GrabOverlay;
using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.TestDoubles;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.Screens.Settings;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの設定ページが言語・走り・画質の節と戻るボタンを出し、戻るがトップへのページ切り替えを送ることを確かめる（L1。Web の PauseMenuPanel.test.ts）
    /// Verifies the pause settings page shows the language, sprint and graphics sections with a back button that requests the top page (L1; Web PauseMenuPanel.test.ts)
    /// </summary>
    public class PauseMenuSettingsPageTest : ScreenTestHost
    {
        private RecordingPauseMenuCommands _commands;
        private PauseMenuView _view;

        [SetUp]
        public void MountPauseMenu()
        {
            _commands = new RecordingPauseMenuCommands();
            _view = Mount(new PauseMenuView(_commands, new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f))));
            _view.SetShown(true);
            _view.Redraw(FakePauseMenuModel.Page(PauseMenuPage.Top), FakeSettingsModel.Default());
            simulate.FrameUpdate();
        }

        /// <web-test file="src/features/pauseMenu/PauseMenuPanel.test.ts">設定画面は言語選択と戻るボタンを出し、戻るはtopへのshow_pageを送る</web-test>
        [Test]
        public void 設定画面は言語選択と戻るボタンを出し戻るはtopへのshow_pageを送る()
        {
            ShowPage(PauseMenuPage.Settings);
            Assert.AreEqual("Settings", _view.Q<Label>(className: "pause-menu__title").PlainText());
            Assert.IsNotNull(_view.Q("language-select"));
            Assert.IsNull(_view.Q("pause-menu-save"));
            Assert.IsInstanceOf<GameScrollView>(Find<VisualElement>(_view, "pause-menu-body"));

            // 言語に続いて走りと画質の節が 1 つずつ並ぶ
            // The sprint and graphics sections follow the language, one each
            Assert.AreEqual(1, _view.Query<SprintModeSelectView>().ToList().Count);
            Assert.IsNotNull(_view.Q("sprint-mode-select"));
            Assert.AreEqual(1, _view.Query<GraphicsQualitySettingsView>().ToList().Count);
            Assert.IsNotNull(_view.Q("graphics-preset"));

            // 戻るは切替を送り枠は中央に置く
            // Back requests a switch, frame is centred
            simulate.Click(Find<Button>(_view, "pause-menu-back"));
            Assert.AreEqual(PauseMenuCommandKind.ShowPage, _commands.Issued.Last().Kind);
            Assert.AreEqual(PauseMenuPage.Top, _commands.Issued.Last().Page);
            Assert.IsTrue(Find<Button>(_view, "pause-menu-back").ClassListContains(AnchorIds.PauseBackToTop));
            Assert.AreEqual(720f - 80f, Frame().worldBound.height, 0.5f);
            Assert.AreEqual(420f, Frame().worldBound.width, 0.5f);
            Assert.AreEqual(640f, Frame().worldBound.center.x, 0.5f);

            #region Internal

            VisualElement Frame()
            {
                return Find<VisualElement>(_view, "pause-menu");
            }

            void ShowPage(PauseMenuPage page)
            {
                // スクロールバーの出し入れで 2 回目のレイアウトが走るので、2 フレーム進めて落ち着かせる
                // Showing or hiding the scroller triggers a second layout, so advance two frames to settle
                _view.RenderPauseMenu(FakePauseMenuModel.Page(page));
                simulate.FrameUpdate();
                simulate.FrameUpdate();
            }

            #endregion
        }
    }
}
