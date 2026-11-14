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
        public void BuildModeOutsideRadius_DeactivatesAndStopsRendering()
        {
            // 照準は当たり判定を使わないので、半径外の部品は有効にしない（ADR 0101）
            // The aim uses no colliders, so out-of-radius parts are not activated (ADR 0101)
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            Assert.IsTrue(fake.Active);
            Assert.IsTrue(fake.Rendered);

            view.SetWithinPlayerRadius(false);
            Assert.IsFalse(fake.Active);
            Assert.IsFalse(fake.Rendered);

            view.SetWithinPlayerRadius(true);
            Assert.IsTrue(fake.Active);
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
        public void EnteringBuildModeOutsideRadius_StaysInactiveWithoutCalls()
        {
            // 半径外のまま設置モードへ入っても、隠れたままの部品には何も呼ばない
            // Entering build mode while out of range leaves the hidden part untouched, with no calls at all
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            var callsAfterConstruction = fake.Calls;
            view.SetWithinPlayerRadius(false);
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            Assert.IsFalse(fake.Active);
            Assert.IsFalse(fake.Rendered);
            Assert.AreEqual(callsAfterConstruction, fake.Calls);

            view.SetDisplay(PreviewOnlyObjectsDisplay.Hidden);
            Assert.AreEqual(callsAfterConstruction, fake.Calls);
        }

        [Test]
        public void SameStateAgain_IssuesNoCalls()
        {
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            var calls = fake.Calls;

            view.SetWithinPlayerRadius(true);
            view.SetDisplay(PreviewOnlyObjectsDisplay.Rendered);
            Assert.AreEqual(calls, fake.Calls);
        }

        [Test]
        public void DebugRendered_IgnoresRadius()
        {
            var fake = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { fake });
            view.SetWithinPlayerRadius(false);
            view.SetDisplay(PreviewOnlyObjectsDisplay.Rendered);
            Assert.IsTrue(fake.Active);
            Assert.IsTrue(fake.Rendered);
        }

        [Test]
        public void LateAddedObject_FollowsCurrentState()
        {
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject>());
            view.SetDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            view.SetWithinPlayerRadius(false);

            var late = new FakePreviewOnlyObject { Active = true, Rendered = true };
            view.Add(late);
            Assert.IsFalse(late.Active);
            Assert.IsFalse(late.Rendered);

            view.SetWithinPlayerRadius(true);
            Assert.IsTrue(late.Active);
            Assert.IsTrue(late.Rendered);
        }

        private sealed class FakePreviewOnlyObject : IPreviewOnlyObject
        {
            public bool Active;
            public bool Rendered;
            public int Calls;
            public void Initialize(BlockId blockId) { }
            public void SetActive(bool active) { Active = active; Calls++; }
            public void SetEnableRenderers(bool enable) { Rendered = enable; Calls++; }
        }
    }
}
