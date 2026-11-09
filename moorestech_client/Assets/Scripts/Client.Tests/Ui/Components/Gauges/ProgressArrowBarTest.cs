using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Gauges;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Gauges
{
    /// <summary>
    /// ProgressArrowBar の名前・寸法・色・重なり・クランプ・つまみを Web の shared/ui/ProgressArrowBar と照らす（Web に vitest は無い）
    /// Checks ProgressArrowBar name, size, colors, layering, clamping and knobs against Web shared/ui/ProgressArrowBar (the Web has no vitest)
    /// </summary>
    public class ProgressArrowBarTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/Gauges/GaugeKnobOverrides.uss";

        [TestCase(-0.2f, 0f)]
        [TestCase(0.5f, 50f)]
        [TestCase(1.4f, 100f)]
        [TestCase(float.NaN, 0f)]
        public void 値を0から1へ丸めて充填の幅にする(float value, float expectedPercent)
        {
            var bar = Mount(rootVisualElement);
            bar.SetValue(value);
            simulate.FrameUpdate();

            var fill = bar.Q(className: "progress-arrow-bar__fill");
            Assert.AreEqual(LengthUnit.Percent, fill.style.width.value.unit);
            Assert.AreEqual(expectedPercent, fill.style.width.value.value, 0.001f);
            Assert.AreEqual(bar.layout.width * expectedPercent / 100f, fill.layout.width, 0.5f);
        }

        [Test]
        public void 名前はWebが固定で付けるtestidで寸法は進捗矢印のトークン()
        {
            var bar = Mount(rootVisualElement);

            Assert.AreEqual("progress-arrow-bar", bar.name);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--progress-arrow-width"), bar.layout.width, 0.5f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--progress-arrow-height"), bar.layout.height, 0.5f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--bevel-1"), bar.resolvedStyle.borderTopLeftRadius, 0.001f);
        }

        [Test]
        public void 溝と線と充填はゲージの色で充填が線を覆い入力を受けない()
        {
            // Web は内側の box-shadow の上に充填が塗られる。後に置いた子が上に描かれる
            // On the Web the fill paints over the inset box-shadow; a later child draws on top
            var bar = Mount(rootVisualElement);
            var outline = bar.Q(className: "progress-arrow-bar__outline");
            var fill = bar.Q(className: "progress-arrow-bar__fill");

            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--gauge-track"), bar.resolvedStyle.backgroundColor, "track");
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--gauge-outline-width"), outline.resolvedStyle.borderTopWidth, 0.001f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--bevel-c1"), outline.resolvedStyle.borderTopColor, "outline");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--gauge-fill"), fill.resolvedStyle.backgroundColor, "fill");
            Assert.AreEqual(0, bar.IndexOf(outline));
            Assert.AreEqual(1, bar.IndexOf(fill));
            Assert.AreEqual(PickingMode.Ignore, bar.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, outline.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, fill.pickingMode);
        }

        [Test]
        public void 祖先でつまみを上書きすると寸法と溝と充填の色が変わる()
        {
            var bar = Mount(ProbeScreen());
            bar.SetValue(1f);
            simulate.FrameUpdate();

            Assert.AreEqual(100f, bar.layout.width, 0.5f);
            Assert.AreEqual(20f, bar.layout.height, 0.5f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--bevel-c1"), bar.Q(className: "progress-arrow-bar__fill").resolvedStyle.backgroundColor, "fill");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--color-content-primary"), bar.resolvedStyle.backgroundColor, "track");
        }

        private VisualElement ProbeScreen()
        {
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            rootVisualElement.styleSheets.Add(probe);
            var screen = new VisualElement();
            screen.AddToClassList("gauge-knob-probe");
            rootVisualElement.Add(screen);
            return screen;
        }

        private ProgressArrowBar Mount(VisualElement parent)
        {
            var bar = new ProgressArrowBar();
            parent.Add(bar);
            simulate.FrameUpdate();
            return bar;
        }
    }
}
