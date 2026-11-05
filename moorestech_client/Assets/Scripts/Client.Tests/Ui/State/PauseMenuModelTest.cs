using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;

namespace Client.Tests.Ui.State
{
    public class PauseMenuModelTest
    {
        [Test]
        public void ShowPageでViewModelのページが変わる()
        {
            var service = new PauseMenuStateService();
            var model = PauseMenuModelTestFactory.Create(service);
            var commands = PauseMenuCommandsTestFactory.Create(service);
            Assert.AreEqual(PauseMenuPage.Top, model.Current.Value.Page);

            commands.ShowPage(PauseMenuPage.Settings);

            Assert.AreEqual(PauseMenuPage.Settings, model.Current.Value.Page);
        }

        [Test]
        public void ポーズを開いた通知が届く()
        {
            var service = new PauseMenuStateService();
            var model = PauseMenuModelTestFactory.Create(service);
            var openedCount = 0;
            using var subscription = UniRx.ObservableExtensions.Subscribe(model.OnPauseMenuOpened, _ => openedCount++);

            service.OnEnter();

            Assert.AreEqual(1, openedCount);
        }
    }
}
