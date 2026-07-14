using Client.Game.InGame.Control.ViewMode;
using NUnit.Framework;

namespace Client.Tests.ViewMode
{
    public class PlayerViewModeControllerTest
    {
        private FakePlayerViewApplier _applier;
        private PlayerViewModeController _controller;

        [SetUp]
        public void SetUp()
        {
            _applier = new FakePlayerViewApplier();
            _controller = new PlayerViewModeController(_applier);
        }

        [Test]
        public void StartAppliesThirdPersonAsInitialMode()
        {
            _controller.Start();

            Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.GetCurrentMode());
            CollectionAssert.AreEqual(
                new[] { PlayerViewMode.ThirdPerson },
                _applier.AppliedModes);
        }

        [Test]
        public void ToggleChangesModeAndAppliesCompleteMode()
        {
            _controller.Start();
            _controller.ToggleViewMode();
            _controller.ToggleViewMode();

            Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.GetCurrentMode());
            CollectionAssert.AreEqual(
                new[]
                {
                    PlayerViewMode.ThirdPerson,
                    PlayerViewMode.FirstPerson,
                    PlayerViewMode.ThirdPerson,
                },
                _applier.AppliedModes);
        }
    }
}
