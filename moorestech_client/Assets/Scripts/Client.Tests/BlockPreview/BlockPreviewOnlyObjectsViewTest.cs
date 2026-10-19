using System.Collections.Generic;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests
{
    /// <summary>
    /// - 表示モードと半径の組合せ検証
    /// - 有効状態と描画状態を確認
    /// - Test combinations of display mode and radius
    /// - Check activation and rendering
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
            // モード外の半径外判定を入場後も維持
            // Preserve the out-of-range decision when entering build mode
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
