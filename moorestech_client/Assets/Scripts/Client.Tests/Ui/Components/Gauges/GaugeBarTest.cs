using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Gauges;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;

namespace Client.Tests.Ui.Components.Gauges
{
    /// <summary>
    /// GaugeBar を Web と照合
    /// Checks GaugeBar against Web
    /// </summary>
    public class GaugeBarTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/ComponentTokenProbe.uss";

        /// <web-test file="src/shared/ui/GaugeBar/index.test.ts">value=$valueを$widthへクランプして描画する</web-test>
        [TestCase(-0.2f, 0f)]
        [TestCase(0.375f, 37.5f)]
        [TestCase(1.4f, 100f)]
        [TestCase(float.NaN, 0f)]
        public void 値を0から1へクランプして充填の幅にする(float value, float expectedPercent)
        {
            var gauge = Mount(rootVisualElement);
            gauge.SetValue(value);
            simulate.FrameUpdate();

            Assert.AreEqual("gauge", gauge.name);
            var fill = gauge.Q(className: "gauge-bar__fill");
            Assert.AreEqual(LengthUnit.Percent, fill.style.width.value.unit);
            Assert.AreEqual(expectedPercent, fill.style.width.value.value, 0.001f);
            Assert.AreEqual(200f * expectedPercent / 100f, fill.worldBound.width, 0.01f);
        }

        [Test]
        public void 高さはスロットの6分の1で溝と線と充填はWebの色()
        {
            var gauge = Mount(rootVisualElement);
            gauge.SetValue(0.5f);
            simulate.FrameUpdate();

            Assert.AreEqual(8f, gauge.worldBound.height, 0.01f);
            Assert.AreEqual(0.782875f, gauge.resolvedStyle.borderTopLeftRadius, 0.001f);
            AssertColor(Rgba(10, 14, 27, 0.8f), gauge.resolvedStyle.backgroundColor, "track");
            var outline = gauge.Q(className: "gauge-bar__outline");
            Assert.AreEqual(1f, outline.resolvedStyle.borderTopWidth, 0.001f);
            AssertColor(Rgba(56, 58, 72, 1f), outline.resolvedStyle.borderTopColor, "outline");
            AssertColor(Rgba(104, 106, 120, 1f), gauge.Q(className: "gauge-bar__fill").resolvedStyle.backgroundColor, "fill");
        }

        [Test]
        public void 充填は線より後ろに置き線を覆い入力を受けない()
        {
            // Web は内側の box-shadow の上に充填が塗られる。後に置いた子が上に描かれる
            // On the Web the fill paints over the inset box-shadow; a later child draws on top
            var gauge = Mount(rootVisualElement);
            var outline = gauge.Q(className: "gauge-bar__outline");
            var fill = gauge.Q(className: "gauge-bar__fill");

            Assert.Less(gauge.IndexOf(outline), gauge.IndexOf(fill));
            Assert.AreEqual(PickingMode.Ignore, gauge.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, outline.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, fill.pickingMode);
        }

        [Test]
        public void 祖先でslot_sizeとgauge_fillを上書きすると高さと充填の色が変わる()
        {
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            rootVisualElement.styleSheets.Add(probe);
            var screen = new VisualElement();
            screen.AddToClassList("component-knob-probe");
            rootVisualElement.Add(screen);
            var gauge = Mount(screen);
            gauge.SetValue(1f);
            simulate.FrameUpdate();

            Assert.AreEqual(16f, gauge.worldBound.height, 0.01f);
            AssertColor(Rgba(0xf2, 0xf3, 0xf7, 1f), gauge.Q(className: "gauge-bar__fill").resolvedStyle.backgroundColor, "fill");
        }

        private GaugeBar Mount(VisualElement parent)
        {
            var host = new VisualElement();
            host.style.width = 200;
            var gauge = new GaugeBar("gauge");
            host.Add(gauge);
            parent.Add(host);
            simulate.FrameUpdate();
            return gauge;
        }
    }
}
