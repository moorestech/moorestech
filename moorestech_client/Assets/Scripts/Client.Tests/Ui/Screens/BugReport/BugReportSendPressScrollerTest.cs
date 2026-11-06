using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 送信ボタンの押下の沈みで、本文がちょうど収まる枠に縦バーが出ないことを確かめる（L1）
    /// Verifies the send button's press sink does not show a vertical bar on a body that exactly fits its frame (L1)
    /// </summary>
    public class BugReportSendPressScrollerTest : ScreenTestHost
    {
        private PauseMenuView _view;

        [SetUp]
        public void MountBugReportPage()
        {
            var model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.BugReport));
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            _view = Mount(new PauseMenuView(model.Current, settings.Current, new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft()));
            _view.SetShown(true);
            Find<TextField>(_view, "bug-report-description").value = "body";
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        [Test]
        public void 送信ボタンを押している間も枠に縦バーを出さない()
        {
            // 前提: 送信ボタンが本文の末尾で、窓の下端にちょうど接している
            // Precondition: the send button ends the body and touches the window bottom exactly
            var body = Find<ScrollView>(_view, "pause-menu-body");
            var send = Find<FormButton>(_view, "bug-report-send");
            Assert.AreEqual(body.contentViewport.layout.height, body.contentContainer.layout.height, 0.5f);
            Assert.AreEqual(body.contentViewport.worldBound.yMax, send.worldBound.yMax, 0.5f);

            // 押下で 1px 沈み、窓の外へ出ても縦バーは出ない
            // The press sinks 1px past the window, yet no vertical bar appears
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
