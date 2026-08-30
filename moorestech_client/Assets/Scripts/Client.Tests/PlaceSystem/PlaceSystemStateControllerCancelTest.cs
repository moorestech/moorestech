using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    /// <summary>
    ///     進行中操作の解除が現在の設置系へ委譲されることを検証
    ///     Verifies that cancelling an in-progress operation is delegated to the current place system
    /// </summary>
    public class PlaceSystemStateControllerCancelTest
    {
        [Test]
        public void 現在の設置系が解除できれば結果をそのまま返す()
        {
            var placeSystem = new CancellablePlaceSystem { CancelResult = true };
            var controller = new PlaceSystemStateController(new SingleSelector(placeSystem), new NullPresenter());
            controller.ManualUpdate();

            Assert.IsTrue(controller.TryCancelInProgressOperation());
            Assert.AreEqual(1, placeSystem.CancelCallCount);
        }

        [Test]
        public void 解除対象が無ければfalseを返す()
        {
            var placeSystem = new CancellablePlaceSystem { CancelResult = false };
            var controller = new PlaceSystemStateController(new SingleSelector(placeSystem), new NullPresenter());
            controller.ManualUpdate();

            Assert.IsFalse(controller.TryCancelInProgressOperation());
        }

        [Test]
        public void ManualUpdate前はEmptyPlaceSystemに委譲されfalseになる()
        {
            var controller = new PlaceSystemStateController(new SingleSelector(new CancellablePlaceSystem { CancelResult = true }), new NullPresenter());

            Assert.IsFalse(controller.TryCancelInProgressOperation());
        }

        private class CancellablePlaceSystem : IPlaceSystem
        {
            public bool CancelResult;
            public int CancelCallCount;
            public bool OwnsWheelInput => false;
            public void Enable() { }
            public void ManualUpdate(PlaceSystemUpdateContext context) { }
            public void Disable() { }

            public bool TryCancelInProgressOperation()
            {
                CancelCallCount++;
                return CancelResult;
            }
        }

        private class SingleSelector : IPlaceSystemSelector
        {
            private readonly IPlaceSystem _placeSystem;

            public SingleSelector(IPlaceSystem placeSystem)
            {
                _placeSystem = placeSystem;
            }

            public IPlaceSystem EmptyPlaceSystem { get; } = new Client.Game.InGame.BlockSystem.PlaceSystem.Empty.EmptyPlaceSystem();

            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
            {
                return _placeSystem;
            }
        }

        private class NullPresenter : IPlacementFeedbackPresenter
        {
            public void Present(PlacementFeedback feedback) { }
            public void Hide() { }
        }
    }
}
