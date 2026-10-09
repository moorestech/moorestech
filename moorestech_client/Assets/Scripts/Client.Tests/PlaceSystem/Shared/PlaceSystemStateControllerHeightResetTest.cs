using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Game.PlacementTarget;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    public class PlaceSystemStateControllerHeightResetTest
    {
        [Test]
        public void 対象が変わると高さが0へ戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var placeSystem = new HeightPlaceSystem();
            var controller = new PlaceSystemStateController(new SingleSelector(placeSystem), new NullPresenter(), heightOffset);

            controller.SetTarget(new BlueprintPlacementTarget(Guid.NewGuid(), "a"), PlacementOrigin.NonHotbar);
            controller.ManualUpdate();
            heightOffset.Adjust(2);
            controller.ManualUpdate();
            Assert.AreEqual(2, heightOffset.Value);

            controller.SetTarget(new BlueprintPlacementTarget(Guid.NewGuid(), "b"), PlacementOrigin.NonHotbar);
            controller.ManualUpdate();
            Assert.AreEqual(0, heightOffset.Value);
        }

        private class HeightPlaceSystem : IPlaceSystem
        {
            public bool OwnsWheelInput => false;
            public bool UsesPlacementHeight => true;
            public void Enable() { }
            public void ManualUpdate(PlaceSystemUpdateContext context) { }
            public void Disable() { }
            public bool TryCancelInProgressOperation() => false;
        }

        private class SingleSelector : IPlaceSystemSelector
        {
            private readonly IPlaceSystem _placeSystem;
            public SingleSelector(IPlaceSystem placeSystem) { _placeSystem = placeSystem; }
            public IPlaceSystem EmptyPlaceSystem { get; } = new Client.Game.InGame.BlockSystem.PlaceSystem.Empty.EmptyPlaceSystem();
            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context) => _placeSystem;
        }

        private class NullPresenter : IPlacementFeedbackPresenter
        {
            public void Present(PlacementFeedback feedback) { }
            public void Hide() { }
        }
    }
}
