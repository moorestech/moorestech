using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.BugReport;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの枠は手で組んだ暗い面なので、中のボタンは面の上と分かる（チュートリアル枠のグローの地。A-136）
    /// The pause frame is a hand-built dark surface, so its buttons count as on a surface (tutorial glow ground; A-136)
    /// </summary>
    public class PauseMenuSurfaceTest : ScreenTestHost
    {
        [Test]
        public void ポーズの枠の中のボタンは面の上と分かる()
        {
            var model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.Top));
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            var view = new PauseMenuView(model.Current, settings.Current, new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft());
            rootVisualElement.Add(view);
            view.SetShown(true);
            simulate.FrameUpdate();

            // 枠そのもの（アンカー pause.menu）は幕の上なので面と数えず、中のボタンは面の上
            // The frame itself (anchor pause.menu) sits over the backdrop, so it is not on a surface; its buttons are
            var panel = Find<VisualElement>(view, "pause-menu");
            Assert.IsFalse(UiSurface.HasSurfaceAncestor(panel));
            Assert.IsTrue(UiSurface.HasSurfaceAncestor(panel.Q<Button>()));
        }
    }
}
