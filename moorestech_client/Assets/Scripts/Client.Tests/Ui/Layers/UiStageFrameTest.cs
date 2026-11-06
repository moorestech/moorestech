using System;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Layers
{
    /// <summary>
    /// 舞台とviewportの寸法を確かめる（L1）
    /// Verifies stage and viewport sizes (L1)
    /// </summary>
    [TestFixture(1280f, 720f, 1f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    public class UiStageFrameTest : UiTestHost
    {
        private const string GameUiPanelSettingsPath = "Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset";
        private static readonly Vector2 StageSize = new(1280f, 720f);
        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;

        public UiStageFrameTest(float width, float height, float panelPixelsPerPoint)
        {
            _logicalPanelSize = new Vector2(width, height);
            _pixelsPerPoint = panelPixelsPerPoint;
        }

        [OneTimeSetUp]
        public void SetUpLogicalPanelSize()
        {
            // 2560×1080は倍率1.5
            // 2560x1080 scales by 1.5
            panelSize = _logicalPanelSize;
        }

        [SetUp]
        public void SetUpPanelPixelsPerPoint()
        {
            // 本番と同じ倍率でレイアウトを画素へ丸める（倍率 1 のままだと 213.33 が 213 に丸まり本番と食い違う）
            // Snap layout to pixels at the production scale (at scale 1, 213.33 snaps to 213 unlike production)
            pixelsPerPoint = _pixelsPerPoint;
        }

        [Test]
        public void StageIsCenteredAndViewportFillsThePanelOnEveryLayer()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            simulate.FrameUpdate();

            var expectedStage = new Rect((_logicalPanelSize - StageSize) / 2f, StageSize);
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                AssertRect(expectedStage, stack.GetLayer(layer, UiFamily.Stage).worldBound, $"{layer} stage");
                AssertRect(new Rect(Vector2.zero, _logicalPanelSize), stack.GetLayer(layer, UiFamily.Viewport).worldBound, $"{layer} viewport");
            }
        }

        [Test]
        public void ProductionPanelSettingsScaleLikeTheWebUiScale()
        {
            // Webのui-scaleと同式
            // Same formula as the Web ui-scale
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(GameUiPanelSettingsPath);
            Assert.IsNotNull(settings, GameUiPanelSettingsPath);
            Assert.AreEqual(PanelScaleMode.ScaleWithScreenSize, settings.scaleMode);
            Assert.AreEqual(PanelScreenMatchMode.Expand, settings.screenMatchMode);
            Assert.AreEqual(new Vector2Int(1280, 720), settings.referenceResolution);
        }

        private static void AssertRect(Rect expected, Rect actual, string label)
        {
            Assert.AreEqual(expected.x, actual.x, 0.05f, $"{label} x");
            Assert.AreEqual(expected.y, actual.y, 0.05f, $"{label} y");
            Assert.AreEqual(expected.width, actual.width, 0.05f, $"{label} width");
            Assert.AreEqual(expected.height, actual.height, 0.05f, $"{label} height");
        }
    }
}
