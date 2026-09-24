using Client.Game.InGame.UI.UIState.State.PauseMenu;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.UIState.PauseMenu
{
    public class PauseMenuPageNavigationTest
    {
        [Test]
        public void 開くたびにトップから始まる()
        {
            var service = new PauseMenuStateService();
            service.ShowPage(PauseMenuPage.BugReport);

            service.OnEnter();

            Assert.AreEqual(PauseMenuPage.Top, service.CurrentPage.Value);
        }

        [Test]
        public void 子画面での閉じキーはトップへ1段戻りポーズを閉じない()
        {
            var service = new PauseMenuStateService();
            service.OnEnter();
            service.ShowPage(PauseMenuPage.Settings);

            var shouldClose = service.StepBackOnCloseKey();

            Assert.IsFalse(shouldClose);
            Assert.AreEqual(PauseMenuPage.Top, service.CurrentPage.Value);
        }

        [Test]
        public void トップでの閉じキーはポーズを閉じる()
        {
            var service = new PauseMenuStateService();
            service.OnEnter();

            Assert.IsTrue(service.StepBackOnCloseKey());
            Assert.AreEqual(PauseMenuPage.Top, service.CurrentPage.Value);
        }

        // Escape時点の記録確保はポーズを開いた瞬間だけ。子画面の行き来で確保し直すと報告が別の瞬間を指す
        // The Escape-moment capture fires only when the pause opens; re-capturing on page moves would point the report elsewhere
        [Test]
        public void 子画面の行き来では開いた通知を出さない()
        {
            var service = new PauseMenuStateService();
            var opened = 0;
            service.OnPauseMenuOpened.Subscribe(_ => opened++);
            service.OnEnter();

            service.ShowPage(PauseMenuPage.BugReport);
            service.StepBackOnCloseKey();
            service.ShowPage(PauseMenuPage.Settings);

            Assert.AreEqual(1, opened);
        }
    }
}
