using Client.Game.InGame.Tutorial;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class WorldPinStateStoreTest
    {
        [Test]
        public void SetPinPublishesAndEpsilonSuppressesJitter()
        {
            var store = new WorldPinStateStore();
            var projection = new WorldPinProjection { ScreenX = 0.5f, ScreenY = 0.5f, OnScreen = true };

            store.SetPin("pin", "text", projection);
            var afterFirstSet = store.GetCurrent();
            Assert.AreEqual(1, afterFirstSet.Pins.Length);
            var revisionAfterSet = afterFirstSet.Revision;

            // ε(0.002)未満の揺れはrevisionを進めないこと
            // Jitter below the ε (0.002) threshold must not advance the revision
            projection.ScreenX = 0.5005f;
            store.SetPin("pin", "text", projection);
            Assert.AreEqual(revisionAfterSet, store.GetCurrent().Revision);

            // ε超の移動はrevisionを進め座標を更新すること
            // Movement beyond ε must advance the revision and update the position
            projection.ScreenX = 0.6f;
            store.SetPin("pin", "text", projection);
            var afterMove = store.GetCurrent();
            Assert.Greater(afterMove.Revision, revisionAfterSet);
            Assert.AreEqual(0.6f, afterMove.Pins[0].ScreenX, 0.0001f);
        }

        [Test]
        public void OnScreenFlipPublishesEvenWithinEpsilon()
        {
            var store = new WorldPinStateStore();
            var projection = new WorldPinProjection { ScreenX = 0.5f, ScreenY = 0.5f, OnScreen = true };
            store.SetPin("pin", "text", projection);
            var revisionBefore = store.GetCurrent().Revision;

            projection.OnScreen = false;
            store.SetPin("pin", "text", projection);
            Assert.Greater(store.GetCurrent().Revision, revisionBefore);
            Assert.IsFalse(store.GetCurrent().Pins[0].OnScreen);
        }

        [Test]
        public void RemovePinClearsAndIsIdempotent()
        {
            var store = new WorldPinStateStore();
            store.SetPin("pin", "text", new WorldPinProjection { OnScreen = true });
            var revisionAfterSet = store.GetCurrent().Revision;

            store.RemovePin("pin");
            var afterRemove = store.GetCurrent();
            Assert.AreEqual(0, afterRemove.Pins.Length);
            Assert.Greater(afterRemove.Revision, revisionAfterSet);

            // 二重Removeはpublishしないこと（冪等）
            // A second remove must not publish (idempotent)
            store.RemovePin("pin");
            Assert.AreEqual(afterRemove.Revision, store.GetCurrent().Revision);
        }
    }
}
