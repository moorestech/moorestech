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
    /// 各層の舞台が論理パネルの中央の 1280×720、実画面の子がパネル全面になることを 720p と 2560×1080 相当で確かめる（L1・設計書 C6）
    /// Verifies each layer's stage is a centered 1280x720 and its viewport child fills the panel, at 720p and the 2560x1080 equivalent (L1, design C6)
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
            // 2560×1080 は Expand（1280×720 基準）で倍率 1.5・論理 1706.67×720 になる
            // 2560x1080 under Expand (1280x720 reference) scales by 1.5 into a 1706.67x720 logical panel
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
            // Web の --ui-scale = min(W/1280, H/720) と同じ式になる設定
            // Settings equivalent to the Web's --ui-scale = min(W/1280, H/720)
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
