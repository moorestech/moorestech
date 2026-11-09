using Client.Tests.Ui.Screens.Tutorial.Overlay;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.Screens.Tutorial.WorldPin;
using Client.Ui.State.Tutorial.Overlay;
using Client.Ui.State.Tutorial.WorldPin;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Tutorial.Cues.WorldPinOverlayTest;
using static Client.Tests.Ui.State.Tutorial.TutorialOverlayProjectionTest;

namespace Client.Tests.Ui.Screens.Tutorial.Cues
{
    /// <summary>
    /// Web e2e の portalLayerScaling.spec を 2 つの論理解像度で写す。2560x1080 は Expand（参照 1280x720）で 1706.7x720 になる
    /// Copies Web e2e portalLayerScaling.spec at two logical sizes; 2560x1080 becomes 1706.7x720 under Expand (reference 1280x720)
    /// </summary>
    [TestFixture(1280f, 720f)]
    [TestFixture(1706.7f, 720f)]
    public class TutorialViewportScalingTest : TutorialOverlayTestHost
    {
        private readonly Vector2 _logicalSize;
        private FakeTutorialOverlayModel _overlay;
        private FakeWorldPinModel _pins;
        private TutorialOverlayView _overlayView;
        private WorldPinOverlayView _pinView;

        public TutorialViewportScalingTest(float width, float height)
        {
            _logicalSize = new Vector2(width, height);
        }

        [OneTimeSetUp]
        public void UseLogicalSize()
        {
            // 基底の OneTimeSetUp（本番の 1280x720）の後に走り、パネルの寸法だけを差し替える
            // Runs after the base OneTimeSetUp (production 1280x720) and only swaps the panel size
            panelSize = _logicalSize;
        }

        [SetUp]
        public void MountLayers()
        {
            _overlay = new FakeTutorialOverlayModel();
            _pins = new FakeWorldPinModel();
            _overlayView = TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, _overlay.Current);
            _pinView = WorldPinPresenter.Mount(rootVisualElement, _pins.Current);
        }

        /// <web-test file="e2e/tests/system/portalLayerScaling.spec.ts">ワールドピンは高解像度で拡大しても先端が射影座標に留まる</web-test>
        [Test]
        public void ワールドピンの先端はどの解像度でも射影座標に留まる()
        {
            _pins.Set(new WorldPinViewModelProjection().Project(Pins(Pin("p1", 0.3f, 0.6f)), false));
            simulate.FrameUpdate();
            var pin = Find<VisualElement>(_pinView, "world-pin-p1").worldBound;
            Assert.AreEqual(_logicalSize.x * 0.3f, pin.center.x, 1f);
            Assert.AreEqual(_logicalSize.y * 0.6f, pin.yMax, 1f);
        }

        /// <web-test file="e2e/tests/system/portalLayerScaling.spec.ts">チュートリアルの枠線ラベルは高解像度で拡大しても枠線に付いたまま</web-test>
        [Test]
        public void 枠線ラベルはどの解像度でも枠に付いたまま()
        {
            MountAnchor(rootVisualElement, "pause.save", _logicalSize.x / 2f - 100f, 300, 200, 40);
            _overlay.Set(new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()))));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var ring = _overlayView.Q(className: "tutorial-outline__ring").worldBound;
            var label = _overlayView.Q<TutorialHighlightLabelView>().worldBound;
            Assert.AreEqual(ring.yMax + Px("--tutorial-highlight-label-gap"), label.yMin, 0.5f);
            Assert.AreEqual(ring.xMin, label.xMin, 0.5f);
        }

        /// <web-test file="e2e/tests/system/portalLayerScaling.spec.ts">画面端矢印は高解像度で拡大しても余白ごと広がり画面内に収まる</web-test>
        [Test]
        public void 画面端の矢印はどの解像度でも余白を保って画面内に収まる()
        {
            _pins.Set(new WorldPinViewModelProjection().Project(Pins(EdgePin("p1", 1f, 0f)), false));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var arrow = Find<VisualElement>(_pinView, "world-pin-arrow-p1").worldBound;
            Assert.AreEqual(_logicalSize.x - Px("--world-pin-edge-margin"), arrow.center.x, 1f);
            Assert.LessOrEqual(arrow.xMax, _logicalSize.x + 0.5f);
        }
    }
}
