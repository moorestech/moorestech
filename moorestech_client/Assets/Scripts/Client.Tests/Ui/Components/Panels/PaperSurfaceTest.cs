using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// PaperSurface が Mantine Paper の既定（面・角丸・枠なし）を写し、つまみの上書きで面と角丸が変わるかを確かめる（L1）
    /// Verifies PaperSurface copies the Mantine Paper defaults (face, radius, no border) and that knob overrides change the face and radius (L1)
    /// </summary>
    public class PaperSurfaceTest : UiTestHost
    {
        private const string KnobSheetPath = "Assets/Scripts/Client.Tests/Ui/Components/Panels/PanelKnobOverrides.uss";
        private VisualElement _screen;
        private PaperSurface _paper;

        [SetUp]
        public void AddPaper()
        {
            var knobSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobSheetPath);
            Assert.IsNotNull(knobSheet, KnobSheetPath);
            rootVisualElement.styleSheets.Add(knobSheet);
            _screen = new VisualElement();
            _paper = new PaperSurface("test-paper");
            _paper.style.width = 240;
            _paper.Add(new Label("First Craft"));
            _screen.Add(_paper);
            rootVisualElement.Add(_screen);
            simulate.FrameUpdate();
        }

        [Test]
        public void 面はmantine_color_bodyで角丸はmantine_radius_smで枠を持たない()
        {
            var style = _paper.resolvedStyle;
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-body"), style.backgroundColor, "background");
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--mantine-radius-sm"), style.borderTopLeftRadius, 0.01f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--mantine-radius-sm"), style.borderBottomRightRadius, 0.01f);
            Assert.AreEqual(0f, style.borderLeftWidth, 0.001f);
        }

        [Test]
        public void 名前はtestidで子は面の中に入る()
        {
            Assert.AreEqual("test-paper", _paper.name);
            Assert.AreSame(_paper, _paper.Q<Label>().hierarchy.parent);
        }

        [Test]
        public void 祖先でつまみpaperを上書きすると面と角丸が変わる()
        {
            _screen.AddToClassList("test-knob-paper");
            simulate.FrameUpdate();
            var style = _paper.resolvedStyle;
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-dark-8"), style.backgroundColor, "background");
            Assert.AreEqual(10f, style.borderTopLeftRadius, 0.01f);
        }
    }
}
