using System;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.TestHost;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.GrabOverlay;
using Client.Ui.State.Inventory.Grab;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.GrabOverlay
{
    /// <summary>
    /// 掴み絵がポインタを中心に 48px で出て、付いて来て、何も拾わないことを確かめる（L1）
    /// Verifies the grab picture shows 48px centered on the pointer, follows it and picks nothing (L1)
    /// </summary>
    [TestFixture(1280f, 720f, 1f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    public class GrabOverlayViewTest : UiTestHost
    {
        private const float Tolerance = 2f;
        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private FakeGrabOverlayPointer _pointer;
        private GameContentPresentation _content;
        private Texture2D _texture;
        private ItemId _wood;

        public GrabOverlayViewTest(float width, float height, float panelPixelsPerPoint)
        {
            _logicalPanelSize = new Vector2(width, height);
            _pixelsPerPoint = panelPixelsPerPoint;
        }

        [OneTimeSetUp]
        public void SetUpLogicalPanelSize()
        {
            panelSize = _logicalPanelSize;
        }

        [SetUp]
        public void SetUp()
        {
            pixelsPerPoint = _pixelsPerPoint;
            _pointer = new FakeGrabOverlayPointer(_pixelsPerPoint);

            // 絵は ID から中身を組むので、マスタはテスト用 mod、テクスチャは手元の 1 枚で Content を組む
            // The view builds content from an id, so Content uses the test mod master and a local texture
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
        }

        [TearDown]
        public void TearDown()
        {
            rootVisualElement.Clear();
            UnityEngine.Object.DestroyImmediate(_texture);
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="e2e/tests/inventory/grabOverlay.spec.ts">アイテムを掴んだ瞬間にオーバーレイがクリック座標へ表示される</web-test>
        [Test]
        public void 掴んだ瞬間に今のポインタを中心に出る()
        {
            var view = Mount();
            _pointer.SetCurrent(new Vector2(300f, 200f));

            view.Render(Holding());
            simulate.FrameUpdate();

            // Web の左上 = 中心 - 24 と同じ（48px の絵の中心がポインタ）
            // Same as Web's top-left = center - 24 (the 48px picture is centered on the pointer)
            Assert.AreEqual(276f, view.worldBound.xMin, Tolerance);
            Assert.AreEqual(176f, view.worldBound.yMin, Tolerance);
            Assert.AreEqual(48f, view.worldBound.width, 0.5f);
            Assert.AreEqual(48f, view.worldBound.height, 0.5f);
        }

        /// <web-test file="e2e/tests/inventory/grabOverlay.spec.ts">拡大表示でもgrab中心がカーソル位置へ追従する</web-test>
        [Test]
        public void 動かしたポインタへ絵の中心が付いて来る()
        {
            var view = Mount();
            view.Render(Holding());
            simulate.FrameUpdate();

            _pointer.Move(new Vector2(900f, 500f));
            simulate.FrameUpdate();

            Assert.AreEqual(900f, view.worldBound.center.x, Tolerance);
            Assert.AreEqual(500f, view.worldBound.center.y, Tolerance);
        }

        [Test]
        public void 隠している間は追わず表示の瞬間に合わせ直す()
        {
            var view = Mount();
            _pointer.Move(new Vector2(100f, 100f));
            _pointer.SetCurrent(new Vector2(640f, 360f));

            view.Render(Holding());
            simulate.FrameUpdate();

            Assert.AreEqual(640f, view.worldBound.center.x, Tolerance);
            Assert.AreEqual(360f, view.worldBound.center.y, Tolerance);
        }

        [Test]
        public void 隠すと見えなくなる()
        {
            var view = Mount();
            view.Render(Holding());
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, view.resolvedStyle.display);

            view.Render(GrabOverlayViewModel.Hidden);
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.None, view.resolvedStyle.display);
        }

        [Test]
        public void 絵の部分木はどの要素も拾わない()
        {
            // 絵がポインタの真下にあるので、拾うと下のスロットの身振りを奪う（Web の pointer-events: none）
            // The picture sits right under the pointer, so picking would steal the slot gestures beneath (Web pointer-events: none)
            var view = Mount();
            _pointer.SetCurrent(new Vector2(300f, 200f));
            view.Render(Holding());
            simulate.FrameUpdate();

            view.Query<VisualElement>().ForEach(element => Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name));
            var picked = view.panel.Pick(new Vector2(300f, 200f));
            Assert.IsFalse(picked != null && (picked == view || view.Contains(picked)), $"拾われた要素: {picked?.name}");
        }

        private GrabOverlayView Mount()
        {
            var layer = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Grab, UiFamily.Viewport);
            var view = new GrabOverlayView(_pointer, _content);
            layer.Add(view);
            simulate.FrameUpdate();
            return view;
        }

        private GrabOverlayViewModel Holding()
        {
            return GrabOverlayViewModel.Holding(_wood, 10);
        }
    }
}
