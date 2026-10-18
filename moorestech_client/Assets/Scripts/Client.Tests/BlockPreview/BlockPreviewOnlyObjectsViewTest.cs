using System.Collections.Generic;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests
{
    /// <summary>
    /// 表示モードと半径内フラグの組み合わせで有効・描画が決まることを確かめる
    /// Verifies active/render state follows the display mode and the within-radius flag
    /// </summary>
    public class BlockPreviewOnlyObjectsViewTest
    {
        [Test]
        public void BuildModeOutsideRadius_KeepsActiveButStopsRendering()
        {
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            Assert.IsTrue(fake.Active);
            Assert.IsTrue(fake.Rendered);

            view.SetWithinPlayerRadius(false);
            Assert.IsTrue(fake.Active, "当たり判定のため有効のまま");
            Assert.IsFalse(fake.Rendered);

            view.SetWithinPlayerRadius(true);
            Assert.IsTrue(fake.Rendered);
        }

        [Test]
        public void RadiusChangeWhileHidden_DoesNotActivate()
        {
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetWithinPlayerRadius(false);
            view.SetWithinPlayerRadius(true);
            Assert.IsFalse(fake.Active);
            Assert.IsFalse(fake.Rendered);
        }

        [Test]
        public void EnteringBuildModeOutsideRadius_StartsUnrendered()
        {
            // 設置モード外で半径外になったブロックは、設置モードに入っても描画しない
            // A block that went out of range outside build mode stays unrendered on entering it
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetWithinPlayerRadius(false);
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            Assert.IsTrue(fake.Active);
            Assert.IsFalse(fake.Rendered);
        }

        [Test]
        public void DebugRendered_IgnoresRadius_AndCollidersOnlyNeverRenders()
        {
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetWithinPlayerRadius(false);
            view.SetDisplay(PreviewOnlyObjectsDisplay.Rendered);
            Assert.IsTrue(fake.Rendered);

            view.SetDisplay(PreviewOnlyObjectsDisplay.CollidersOnly);
            Assert.IsTrue(fake.Active);
            Assert.IsFalse(fake.Rendered);
        }

        [Test]
        public void LateAddedObject_FollowsCurrentState()
        {
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject>());
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            view.SetWithinPlayerRadius(false);

            var late = new FakePreviewOnlyObject();
            view.Add(late);
            Assert.IsTrue(late.Active);
            Assert.IsFalse(late.Rendered);
        }

        private sealed class FakePreviewOnlyObject : IPreviewOnlyObject
        {
            public bool Active;
            public bool Rendered;
            public void Initialize(BlockId blockId) { }
            public void SetActive(bool active) { Active = active; }
            public void SetEnableRenderers(bool enable) { Rendered = enable; }
        }
    }
}
