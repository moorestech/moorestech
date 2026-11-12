using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.Skit;
using Client.Ui.Screens.Skit.Parts.Toolbar;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// 横長画面で会話帯が実画面の全幅に広がり、ツールが実画面の右上に付くことを確かめる（L1。Web の 2432×786 相当と 2560×1080 相当）
    /// Verifies that on wide screens the dialogue band spans the full viewport width and the tools pin to the viewport's top-right (L1; Web 2432x786 and 2560x1080 equivalents)
    /// </summary>
    [TestFixture(2227.79f, 720f, 1.0917f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    public class SkitScreenWideLayoutTest : ScreenTestHost
    {
        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;

        public SkitScreenWideLayoutTest(float width, float height, float panelPixelsPerPoint)
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
        public void SetUpPanelPixelsPerPoint()
        {
            pixelsPerPoint = _pixelsPerPoint;
        }

        /// <web-test file="e2e/tests/skit.spec.ts">横長画面で会話帯を全幅に広げツールを実画面右上へ固定する</web-test>
        [Test]
        public void 横長画面で会話帯を全幅に広げツールを実画面右上へ固定する()
        {
            var view = Mount(new SkitScreenView(new RecordingSkitCommands(), Toaster, new UiOsCursorRequestStack(new FakeUiOsCursorApplier())));
            view.Render(SkitTestData.Text(SkitTestData.SessionId, 1, "Blocking message", SkitTestData.TextIntents));
            simulate.FrameUpdate();

            // 帯は左端 0 から実画面の右端まで、下端は実画面の下端
            // The band runs from x 0 to the viewport's right edge and its bottom is the viewport bottom
            var band = Find<VisualElement>(view, "skit-panel").worldBound;
            Assert.AreEqual(0f, band.xMin, 0.5f);
            Assert.AreEqual(_logicalPanelSize.x, band.xMax, 0.5f);
            Assert.AreEqual(_logicalPanelSize.y, band.yMax, 0.5f);

            // ツールは実画面の右上から上 8.7・右 31.3（Web の「右の隙間 0〜40px・上 0〜20px」をトークンの値で締める。D-20）
            // Tools sit 8.7 from the top and 31.3 from the right (the Web "right gap 0-40px, top 0-20px" tightened to token values; D-20)
            var hideUi = Find<VisualElement>(view, "skit-tool-hide-ui").worldBound;
            var auto = Find<VisualElement>(view, "skit-tool-auto").worldBound;
            Assert.AreEqual(31.3f, _logicalPanelSize.x - hideUi.xMax, 0.5f);
            Assert.AreEqual(8.7f, auto.yMin, 0.5f);

            // 図形は不透明で、影の色が解けている（Web の filter: drop-shadow と opacity 1）
            // The glyph is opaque and its shadow color resolves (Web filter: drop-shadow and opacity 1)
            var glyph = view.Q<SkitToolGlyph>();
            Assert.AreEqual(1f, Find<VisualElement>(view, "skit-tool-auto").resolvedStyle.opacity);
            Assert.IsTrue(glyph.customStyle.TryGetValue(new CustomStyleProperty<Color>("--skit-tool-glyph-shadow-color"), out var shadow));
            Assert.Greater(shadow.a, 0f);
        }
    }
}
