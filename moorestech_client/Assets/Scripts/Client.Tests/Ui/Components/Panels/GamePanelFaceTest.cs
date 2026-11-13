using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// GamePanel（default）の面のはみ出し・フェードと題の transform が、既定とつまみの上書きで Web と同じになるかを確かめる（L1）
    /// Verifies the GamePanel (default) face overhang, fades and title transform match the Web with defaults and knob overrides (L1)
    /// </summary>
    public class GamePanelFaceTest : UiTestHost
    {
        private const string KnobSheetPath = "Assets/Scripts/Client.Tests/Ui/Components/Panels/PanelKnobOverrides.uss";

        // Web の calc(var(--panel-top, -8px) - 3.913894px) の定数部
        // The constant part of the Web calc(var(--panel-top, -8px) - 3.913894px)
        private const float WebTopShift = 3.913894f;

        private VisualElement _screen;
        private GamePanel _panel;

        [SetUp]
        public void AddPanel()
        {
            Localize.Initialize();
            var knobSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobSheetPath);
            Assert.IsNotNull(knobSheet, KnobSheetPath);
            rootVisualElement.styleSheets.Add(knobSheet);

            // 画面のルートに当たる祖先の下にパネルを置き、つまみはその祖先で変える
            // Put the panel under an ancestor standing in for a screen root; knobs change on that ancestor
            _screen = new VisualElement();
            _panel = new GamePanel("test-game-panel", GamePanelFrame.Default);
            _panel.style.width = 400;
            _panel.style.height = 300;
            _panel.SetTitle(new UiText(LocalizationKeys.Ui.BlockInventory.TrainInventory));
            _screen.Add(_panel);
            rootVisualElement.Add(_screen);
        }

        [Test]
        public void 面は既定で上へ約12px左右へ3px下へ8pxはみ出す()
        {
            simulate.FrameUpdate();
            var panel = _panel.worldBound;
            var face = Face().worldBound;
            Assert.AreEqual(panel.y - 8f - WebTopShift, face.y, 0.5f);
            Assert.AreEqual(panel.x - 3f, face.x, 0.5f);
            Assert.AreEqual(panel.xMax + 3f, face.xMax, 0.5f);
            Assert.AreEqual(panel.yMax + 8f, face.yMax, 0.5f);
        }

        // 受け取った幅での描画結果（縁の重みの掛け算）は GamePanelFaceOpacityRampTest が L0 で確かめる
        // The drawn result for the received spans (edge weights multiplied in) is checked at L0 by GamePanelFaceOpacityRampTest
        [Test]
        public void 面は面色と左右のフェード幅をトークンから受け取り上下のフェード幅はWebの値()
        {
            simulate.FrameUpdate();
            var style = Face().customStyle;
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(style, "--game-panel-face", out var faceColor));
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--surface-navy"), faceColor, "--game-panel-face");
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(style, "--game-panel-edge-fade", out var sideFade));
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--panel-edge-fade"), sideFade, 0.0001f);
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(style, "--game-panel-top-fade", out var topFade));
            Assert.AreEqual(11.742f, topFade, 0.0001f);
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(style, "--game-panel-bottom-fade", out var bottomFade));
            Assert.AreEqual(12f, bottomFade, 0.0001f);
        }

        // 三角の帯は面の兄弟なので、下端のフェードはパネルのルートから継いで面と同じ値を読む
        // The triangle band is the face's sibling, so it inherits the bottom fade from the panel root and reads the face's value
        [Test]
        public void 三角の帯は三角の幅と下端のフェード幅をUSSから受け取り面と同じフェード幅になる()
        {
            simulate.FrameUpdate();
            var style = _panel.Q(className: "game-panel__bottom-deco").customStyle;
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(style, "--game-panel-deco-triangle-width", out var triangleWidth));
            Assert.AreEqual(68f, triangleWidth, 0.0001f);
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(style, "--game-panel-bottom-fade", out var decoFade));
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(Face().customStyle, "--game-panel-bottom-fade", out var faceFade));
            Assert.AreEqual(faceFade, decoFade, 0.0001f);
        }

        [Test]
        public void 祖先でつまみpanel_leftを上書きすると面の左のはみ出しが変わる()
        {
            _screen.AddToClassList("test-knob-panel-left");
            simulate.FrameUpdate();
            Assert.AreEqual(_panel.worldBound.x - 2.22f, Face().worldBound.x, 0.5f);
        }

        [Test]
        public void 題の倍率は既定で0点947でずらしは0()
        {
            simulate.FrameUpdate();
            var title = Title().resolvedStyle;
            Assert.AreEqual(0.947f, title.scale.value.x, 0.001f);
            Assert.AreEqual(0.947f, title.scale.value.y, 0.001f);
            Assert.AreEqual(0f, title.translate.x, 0.001f);
        }

        [Test]
        public void 祖先でつまみtitleを上書きすると題の倍率とずらしが変わる()
        {
            _screen.AddToClassList("test-knob-title");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var title = Title().resolvedStyle;
            Assert.AreEqual(0.919f, title.scale.value.x, 0.001f);
            Assert.AreEqual(0.924f, title.scale.value.y, 0.001f);
            Assert.AreEqual(-1.96f, title.translate.x, 0.001f);
        }

        private VisualElement Face()
        {
            return _panel.Q(className: "game-panel__face");
        }

        private VisualElement Title()
        {
            return _panel.Q(className: "game-panel__title");
        }
    }
}
