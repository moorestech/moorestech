using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    /// <summary>
    ///     理由の収集・表示・離脱時の消去という本PRの中核配線を検証
    ///     Verifies this PR's core wiring: collect reasons, present them, and clear them on leave
    /// </summary>
    public class PlaceSystemStateControllerFeedbackTest
    {
        [Test]
        public void 毎フレーム理由を集め直してから表示する()
        {
            var placeSystem = new FakePlaceSystem();
            var presenter = new FakePlacementFeedbackPresenter();
            var controller = new PlaceSystemStateController(new FakePlaceSystemSelector(placeSystem), presenter);

            controller.ManualUpdate();
            controller.ManualUpdate();

            // 前フレームの行が残っていれば2フレーム目は2行になる。1行のままであることがClear実行の証拠
            // A leftover line from the previous frame would make frame 2 hold two lines; staying at one proves Clear ran
            Assert.AreEqual(2, presenter.PresentedLineCounts.Count);
            Assert.AreEqual(1, presenter.PresentedLineCounts[0]);
            Assert.AreEqual(1, presenter.PresentedLineCounts[1]);
            Assert.AreEqual(0, presenter.HideCount);
        }

        [Test]
        public void 設置モード離脱で理由表示を消す()
        {
            var presenter = new FakePlacementFeedbackPresenter();
            var controller = new PlaceSystemStateController(new FakePlaceSystemSelector(new FakePlaceSystem()), presenter);

            controller.ManualUpdate();
            controller.Disable();

            Assert.AreEqual(1, presenter.HideCount);
        }

        [Test]
        public void 構築時点では表示面へ触らない()
        {
            var presenter = new FakePlacementFeedbackPresenter();

            var controller = new PlaceSystemStateController(new FakePlaceSystemSelector(new FakePlaceSystem()), presenter);

            Assert.IsNotNull(controller);
            Assert.AreEqual(0, presenter.HideCount);
            Assert.AreEqual(0, presenter.PresentedLineCounts.Count);
        }

        private class FakePlaceSystem : IPlaceSystem
        {
            public bool OwnsWheelInput => false;

            public void Enable() { }

            public void ManualUpdate(PlaceSystemUpdateContext context)
            {
                context.Feedback.AddTooFar();
            }

            public void Disable() { }

            public bool TryCancelInProgressOperation() => false;
        }

        private class FakePlaceSystemSelector : IPlaceSystemSelector
        {
            private readonly IPlaceSystem _placeSystem;

            public FakePlaceSystemSelector(IPlaceSystem placeSystem)
            {
                _placeSystem = placeSystem;
                EmptyPlaceSystem = new FakePlaceSystem();
            }

            public IPlaceSystem EmptyPlaceSystem { get; }

            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
            {
                return _placeSystem;
            }
        }

        private class FakePlacementFeedbackPresenter : IPlacementFeedbackPresenter
        {
            public readonly List<int> PresentedLineCounts = new();
            public int HideCount { get; private set; }

            public void Present(PlacementFeedback feedback)
            {
                PresentedLineCounts.Add(feedback.Lines.Count);
                if (0 < feedback.Lines.Count) Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTooFar.Key, feedback.Lines[0].Key.Key);
            }

            public void Hide()
            {
                HideCount++;
            }
        }
    }
}
