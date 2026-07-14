using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.UIState;
using NUnit.Framework;

namespace Client.Tests.ViewMode
{
    public class PlayerViewTextInputFocusTest
    {
        private FakePlayerViewApplier _applier;
        private PlayerViewModeController _controller;

        [SetUp]
        public void SetUp()
        {
            _applier = new FakePlayerViewApplier();
            _controller = new PlayerViewModeController(_applier);
        }

        [TearDown]
        public void TearDown()
        {
            AimPointProvider.SetMode(AimPointMode.Mouse);
        }

        [Test]
        public void TextInputFocusInFirstPersonFreesCursorAndRestoresOnUnfocus()
        {
            _controller.SetUIState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();

            _controller.ManualUpdate(true);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCameraRotatable);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.CurrentMode);

            _controller.ManualUpdate(false);
            Assert.AreEqual(false, _applier.LastCursorVisible);
            Assert.AreEqual(true, _applier.LastCameraRotatable);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.CurrentMode);
        }

        [Test]
        public void TextInputFocusInThirdPersonIsNoOp()
        {
            _controller.SetUIState(UIStateEnum.PlaceBlock);
            var callCount = _applier.Calls.Count;

            _controller.ManualUpdate(true);
            _controller.ManualUpdate(false);
            Assert.AreEqual(callCount, _applier.Calls.Count);
        }
    }
}
