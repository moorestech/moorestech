using Client.Game.InGame.Control.ViewMode;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.ViewMode
{
    public class PlayerViewModeInputTest : InputTestFixture
    {
        private PlayerViewModeController _controller;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _controller = new PlayerViewModeController(new FakePlayerViewApplier());
            _controller.Start();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [Test]
        public void TickTogglesViewModeWhenVIsPressed()
        {
            Press(_keyboard.vKey);
            _controller.Tick();

            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.GetCurrentMode());
        }

        [Test]
        public void TickDoesNotToggleForAnotherKey()
        {
            Press(_keyboard.bKey);
            _controller.Tick();

            Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.GetCurrentMode());
        }
    }
}
