using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 送信ボタン押下の沈みで縦バーが出ないことを確かめる（L1）
    /// Verifies the send press sink shows no vertical bar (L1)
    /// </summary>
    public class BugReportSendPressScrollerTest : ScreenTestHost
    {
        private PauseMenuView _view;

        [SetUp]
        public void MountBugReportPage()
        {
            var model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.BugReport));
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            _view = Mount(new PauseMenuView(model.Current, settings.Current, new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier())));
            _view.SetShown(true);
            Find<TextField>(_view, "bug-report-description").value = "body";
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        [Test]
        public void 送信ボタンを押している間も枠に縦バーを出さない()
        {
            // 前提: 送信ボタンが窓の下端に接している
            // Precondition: the send button touches the window bottom
            var body = Find<ScrollView>(_view, "pause-menu-body");
            var send = Find<FormButton>(_view, "bug-report-send");
            Assert.AreEqual(body.contentViewport.layout.height, body.contentContainer.layout.height, 0.5f);
            Assert.AreEqual(body.contentViewport.worldBound.yMax, send.worldBound.yMax, 0.5f);

            // 押下で 1px 沈んでも縦バーは出ない
            // The 1px press sink shows no vertical bar
            simulate.MouseDown(send);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(1f, send.resolvedStyle.translate.y, 0.01f);
            Assert.Greater(send.worldBound.yMax, body.contentViewport.worldBound.yMax);
            Assert.AreEqual(DisplayStyle.None, body.verticalScroller.resolvedStyle.display);

            simulate.MouseUp(send);
            simulate.FrameUpdate();
        }
    }
}
