using System.IO;
using Client.Game.InGame.Tutorial;
using Client.Tests.Ui.State.Tutorial;
using Client.Ui.Screens.Tutorial.WorldPin;
using Client.Ui.State.Tutorial.WorldPin;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Cues
{
    /// <summary>
    /// 画面内のマーカーと画面端の矢印を出すワールドピンの L1
    /// L1 for world pins: on-screen markers and edge arrows
    /// </summary>
    public class WorldPinOverlayTest : ScreenTestHost
    {
        private FakeWorldPinModel _model;
        private WorldPinOverlayView _view;

        [SetUp]
        public void MountOverlay()
        {
            _model = new FakeWorldPinModel();
            _view = WorldPinPresenter.Mount(rootVisualElement, _model.Current);
        }

        internal static WorldPinPresentationData Pins(params WorldPinData[] pins)
        {
            return new WorldPinPresentationData { Revision = 1, Pins = pins };
        }

        internal static WorldPinData Pin(string pinId, float screenX, float screenY)
        {
            return new WorldPinData { PinId = pinId, TutorialGuid = TutorialOverlayProjectionTest.LabelGuid.ToString(), ScreenX = screenX, ScreenY = screenY, OnScreen = true };
        }

        internal static WorldPinData EdgePin(string pinId, float directionX, float directionY)
        {
            return new WorldPinData { PinId = pinId, TutorialGuid = TutorialOverlayProjectionTest.LabelGuid.ToString(), OnScreen = false, DirectionX = directionX, DirectionY = directionY };
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">on-screen world pin renders with its marker tip at the projected viewport position</web-test>
        [Test]
        public void 画面内のピンはマーカーの先端を射影座標に置く()
        {
            _model.Set(new WorldPinViewModelProjection().Project(Pins(Pin("p1", 0.5f, 0.4f)), false));
            simulate.FrameUpdate();

            var pin = Find<VisualElement>(_view, "world-pin-p1");
            Assert.AreEqual(640f, pin.worldBound.center.x, 1f);
            Assert.AreEqual(288f, pin.worldBound.yMax, 1f);
        }

        [Test]
        public void マーカーは20pxの箱で切り描画は箱の中の子が持つ()
        {
            // Web の svg は viewBox の上へはみ出た頂を切る。UITK は箱の overflow で子の描画を切って揃える
            // The Web svg clips the apex above its viewBox; UITK clips the child's drawing with the box's overflow
            _model.Set(new WorldPinViewModelProjection().Project(Pins(Pin("p1", 0.5f, 0.4f)), false));
            simulate.FrameUpdate();

            var marker = Find<VisualElement>(_view, "world-pin-p1").Q(className: "world-pin__marker");
            var glyph = marker.Q(className: "world-pin__marker-glyph");
            // overflow は resolvedStyle に無いので USS の規則で見る（前例 ModeHudDesignTest の USS 読み）
            // resolvedStyle has no overflow, so check the USS rule (precedent: ModeHudDesignTest reading USS)
            var uss = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Client.Ui/Screens/Tutorial/WorldPin/WorldPin.uss"));
            StringAssert.IsMatch(@"\.world-pin__marker\s*\{[^}]*overflow:\s*hidden;", uss);
            Assert.AreSame(marker, glyph.parent);
            Assert.AreEqual(20f, glyph.layout.width, 0.01f);
            Assert.AreEqual(20f, glyph.layout.height, 0.01f);
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">world pin follows updated projections</web-test>
        [Test]
        public void 射影が更新されるとピンが追従する()
        {
            _model.Set(new WorldPinViewModelProjection().Project(Pins(Pin("p1", 0.5f, 0.4f)), false));
            simulate.FrameUpdate();
            _model.Set(new WorldPinViewModelProjection().Project(Pins(Pin("p1", 0.25f, 0.75f)), false));
            simulate.FrameUpdate();

            var pin = Find<VisualElement>(_view, "world-pin-p1");
            Assert.AreEqual(320f, pin.worldBound.center.x, 1f);
            Assert.AreEqual(540f, pin.worldBound.yMax, 1f);
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">off-screen world pin renders a 56px filled shaft arrow at the screen edge</web-test>
        [Test]
        public void 画面外のピンは端に56pxの矢印を出す()
        {
            _model.Set(new WorldPinViewModelProjection().Project(Pins(EdgePin("p1", 1f, 0f)), false));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var arrow = Find<VisualElement>(_view, "world-pin-arrow-p1");
            Assert.AreEqual((56f, 56f), (arrow.resolvedStyle.width, arrow.resolvedStyle.height));
            Assert.AreEqual(1280f - 40f, arrow.worldBound.center.x, 1f);
            Assert.AreEqual(360f, arrow.worldBound.center.y, 1f);
            Assert.IsNull(_view.Q("world-pin-p1"));
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">off-screen arrow follows a diagonal direction to the corner region</web-test>
        [Test]
        public void 斜めの方向は角の領域へ向き角度も回す()
        {
            _model.Set(new WorldPinViewModelProjection().Project(Pins(EdgePin("p1", 0.7071f, 0.7071f)), false));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var arrow = Find<VisualElement>(_view, "world-pin-arrow-p1");
            Assert.Greater(arrow.worldBound.center.x, 640f);
            Assert.AreEqual(720f - 40f, arrow.worldBound.center.y, 1f);
            Assert.AreEqual(45f, arrow.resolvedStyle.rotate.angle.ToDegrees(), 0.5f);
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">off-screen arrow remains inside the viewport at all four diagonal corners</web-test>
        [TestCase(1f, 1f)]
        [TestCase(-1f, 1f)]
        [TestCase(1f, -1f)]
        [TestCase(-1f, -1f)]
        public void 四隅の斜め方向でも矢印は画面内に収まる(float dx, float dy)
        {
            _model.Set(new WorldPinViewModelProjection().Project(Pins(EdgePin("p1", dx * 0.7071f, dy * 0.7071f)), false));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var bound = Find<VisualElement>(_view, "world-pin-arrow-p1").worldBound;
            Assert.GreaterOrEqual(bound.xMin, -0.5f);
            Assert.GreaterOrEqual(bound.yMin, -0.5f);
            Assert.LessOrEqual(bound.xMax, 1280.5f);
            Assert.LessOrEqual(bound.yMax, 720.5f);
        }

        /// <web-test file="e2e/tests/system/worldPin.spec.ts">clearing world pins removes the overlay</web-test>
        [Test]
        public void ピンが0件になるとオーバーレイごと消える()
        {
            _model.Set(new WorldPinViewModelProjection().Project(Pins(Pin("p1", 0.5f, 0.5f)), false));
            simulate.FrameUpdate();
            _model.Set(new WorldPinViewModelProjection().Project(Pins(), false));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view));
        }

        [Test]
        public void 画面内から画面外へ切り替わったピンはマーカーを外して矢印を出す()
        {
            _model.Set(new WorldPinViewModelProjection().Project(Pins(Pin("p1", 0.5f, 0.4f)), false));
            simulate.FrameUpdate();
            Assert.IsNotNull(_view.Q("world-pin-p1"));

            _model.Set(new WorldPinViewModelProjection().Project(Pins(EdgePin("p1", 1f, 0f)), false));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            Assert.IsNull(_view.Q("world-pin-p1"));
            Assert.IsTrue(IsShown(_view.Q("world-pin-arrow-p1")));
        }

        [Test]
        public void 消えたピンのマーカーだけが外れる()
        {
            _model.Set(new WorldPinViewModelProjection().Project(Pins(Pin("p1", 0.5f, 0.4f), Pin("p2", 0.2f, 0.2f)), false));
            simulate.FrameUpdate();
            _model.Set(new WorldPinViewModelProjection().Project(Pins(Pin("p2", 0.2f, 0.2f)), false));
            simulate.FrameUpdate();

            Assert.IsNull(_view.Q("world-pin-p1"));
            Assert.IsNotNull(_view.Q("world-pin-p2"));
        }

        [Test]
        public void 同じピンIDでも読めなかったGUIDの後に正しいGUIDが来ればピンが出る()
        {
            var projection = new WorldPinViewModelProjection();
            var broken = Pin("p1", 0.5f, 0.4f);
            broken.TutorialGuid = "not-a-guid";
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("tutorialGuid is not a GUID: 'not-a-guid'"));
            _model.Set(projection.Project(Pins(broken), false));
            _model.Set(projection.Project(Pins(Pin("p1", 0.5f, 0.4f)), false));
            simulate.FrameUpdate();

            Assert.IsNotNull(_view.Q("world-pin-p1"));
        }
    }
}
