using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;

namespace Client.Tests.Ui.State
{
    public class PauseMenuModelTest
    {
        [TearDown]
        public void TearDown()
        {
            PlaytestDeliveryStatusBoard.ResetOnPlayMode();
            PlaytestExportStatusBoard.ResetOnPlayMode();
        }

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

        [Test]
        public void 送信の板の変化はモデルを作った後でも現在値に載る()
        {
            var model = PauseMenuModelTestFactory.Create(new PauseMenuStateService());

            // 待ち・拒否・全送信と順に変えるたびに同じものが載る
            // Each change through pending, refused and all-sent lands as the same value
            foreach (var status in new PlaytestDeliveryStatus[] { new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(2, 3)), new PlaytestDeliveryRefused(new PlaytestOutboxDeliveryCounts(1, 4)), PlaytestDeliveryAllSent.Instance })
            {
                PlaytestDeliveryStatusBoard.Set(status);
                Assert.AreSame(status, model.Current.Value.Delivery);
            }
        }

        [Test]
        public void 書き出しの板の変化はモデルを作った後でも現在値に載る()
        {
            var model = PauseMenuModelTestFactory.Create(new PauseMenuStateService());

            PlaytestExportStatusBoard.Set(PlaytestExportRunning.Instance);

            Assert.AreSame(PlaytestExportRunning.Instance, model.Current.Value.Export);
        }
    }
}
