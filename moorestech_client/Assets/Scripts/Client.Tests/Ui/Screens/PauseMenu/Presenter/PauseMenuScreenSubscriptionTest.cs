using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.Tests.Ui.Screens.GrabOverlay;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.State;
using Client.Tests.Ui.TestDoubles;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Client.Tests.Ui.Screens.PauseMenu.Presenter
{
    /// <summary>
    /// ポーズの Model・言語の購読が表示中だけであることを本物の Presenter で確かめる（§4.2 の ①。前例 InventoryScreenVisibilityTest）
    /// Verifies with the real presenter that the pause model and language are followed only while shown (§4.2 type 1; precedent InventoryScreenVisibilityTest)
    /// </summary>
    public class PauseMenuScreenSubscriptionTest : ScreenTestHost
    {
        private GameObject _host;
        private UIStateControl _control;
        private PauseMenuStateService _pauseMenuState;
        private PauseMenuView _view;

        [SetUp]
        public void MountPresenter()
        {
            Localize.TrySetChosenLanguage("english");
            _host = new GameObject("pause-menu-subscription");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.PauseMenu }, out _control);
            _pauseMenuState = new PauseMenuStateService();
            var root = GameUiRootTestFactory.Create(_host, rootVisualElement);
            new PauseMenuScreenPresenter(root, routing, PauseMenuModelTestFactory.Create(_pauseMenuState), new SettingsModel(), new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, new BugReportDraft(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f))).Initialize();
            _view = rootVisualElement.Q<PauseMenuView>();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DestroyHost()
        {
            Localize.TrySetChosenLanguage("english");
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void 隠れている間にModelが変わっても描き直さない()
        {
            // トップを出してから隠し、隠れている間に設定のページへ変える
            // Show the top page, hide it, then switch to the settings page while hidden
            OpenPause();
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            _pauseMenuState.ShowPage(PauseMenuPage.Settings);
            Localize.TrySetChosenLanguage("japanese");
            simulate.FrameUpdate();

            Assert.IsFalse(IsShown(_view));
            Assert.IsNotNull(_view.Q("pause-menu-save"), "隠れている間に設定のページへ描き直した");
            Assert.IsNull(_view.Q("language-select"));
            Assert.IsNotNull(_view.Q("pause-menu-locale-english"), "隠れている間に言語の変化を描いた");
        }

        [Test]
        public void 出たら最新の値で1回描く()
        {
            // 隠れている間の変化は、出た直後の 1 回描きで最新のページと言語になる
            // Changes made while hidden appear as the latest page and language in the one draw on show
            OpenPause();
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            _pauseMenuState.ShowPage(PauseMenuPage.Settings);
            Localize.TrySetChosenLanguage("japanese");
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.PauseMenu);
            simulate.FrameUpdate();

            Assert.IsTrue(IsShown(_view));
            Assert.IsNotNull(_view.Q("language-select"), "出しても最新のページで描き直さない");
            Assert.IsNull(_view.Q("pause-menu-save"));
            Assert.IsNotNull(_view.Q("pause-menu-locale-japanese"), "出しても最新の言語で描き直さない");

            // 出ている間は言語の変化だけでロケール印を差し替える
            // While shown, a language change alone swaps the locale marker
            Localize.TrySetChosenLanguage("english");
            Assert.IsNotNull(_view.Q("pause-menu-locale-english"), "出ている間の言語の変化が届かない");
        }

        // 遷移後に開いたことを通知する（開くとページはトップへ戻る）
        // Announce the open after the transition (opening returns to the top page)
        private void OpenPause()
        {
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.PauseMenu);
            _pauseMenuState.OnEnter();
            simulate.FrameUpdate();
        }
    }
}
