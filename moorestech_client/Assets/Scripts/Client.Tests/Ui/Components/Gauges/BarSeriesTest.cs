using System.Linq;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Gauges;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Gauges
{
    /// <summary>
    /// BarSeries の並び・クランプ・空の棒・寸法・色・つまみを Web の shared/ui/BarSeries と照らす
    /// Checks BarSeries order, clamping, empty bars, size, colors and knobs against Web shared/ui/BarSeries
    /// </summary>
    public class BarSeriesTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/Gauges/GaugeKnobOverrides.uss";

        /// <web-test file="src/shared/ui/BarSeries/index.test.ts">renders one bar per value with clamped heights, oldest first</web-test>
        [Test]
        public void 値ごとに1本の棒を古い順に左から並べ高さを0から1へ丸める()
        {
            var series = Mount(rootVisualElement, new float?[] { -0.2f, 0.375f, 1.4f, float.NaN });

            Assert.AreEqual("bars", series.name);
            CollectionAssert.AreEqual(new[] { 0f, 37.5f, 100f, 0f }, FillPercents(series));
            var bars = Bars(series);
            for (var i = 1; i < bars.Length; i++) Assert.Greater(bars[i].worldBound.x, bars[i - 1].worldBound.x, $"bar {i}");
        }

        /// <web-test file="src/shared/ui/BarSeries/index.test.ts">marks a null value as an empty bar with only its track</web-test>
        [Test]
        public void nullの値は溝だけの空の棒として印を付ける()
        {
            var series = Mount(rootVisualElement, new float?[] { null, 0.5f });

            var bars = Bars(series);
            Assert.AreEqual(1, bars.Count(bar => bar.ClassListContains("bar-series__bar--empty")));
            Assert.IsTrue(bars[0].ClassListContains("bar-series__bar--empty"));
            CollectionAssert.AreEqual(new[] { 0f, 50f }, FillPercents(series));
        }

        [Test]
        public void 棒は固定の幅と高さで先頭以外は左に隙間を持つ()
        {
            var series = Mount(rootVisualElement, new float?[] { 0.2f, 0.4f, 0.6f });

            var bars = Bars(series);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--bar-series-height"), series.layout.height, 0.5f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--bar-series-bar-width"), bars[0].layout.width, 0.5f);
            Assert.AreEqual(series.layout.height, bars[0].layout.height, 0.5f);
            Assert.AreEqual(0f, bars[0].resolvedStyle.marginLeft, 0.001f);
            var gap = ThemeTokenFile.ResolvePixels("--bar-series-gap");
            Assert.AreEqual(gap, bars[1].layout.x - bars[0].layout.xMax, 0.5f);
            Assert.AreEqual(gap, bars[2].layout.x - bars[1].layout.xMax, 0.5f);
            Assert.AreEqual(bars[0].layout.height * 0.2f, bars[0].Q(className: "bar-series__bar-fill").layout.height, 0.5f);
        }

        [Test]
        public void 値の数が変わると0本まで棒を足し引きし先頭の印は最初の棒だけに付く()
        {
            var series = Mount(rootVisualElement, new float?[] { 0.1f, 0.2f, 0.3f });
            series.SetValues(new float?[0]);
            simulate.FrameUpdate();
            Assert.AreEqual(0, Bars(series).Length);

            series.SetValues(new float?[] { null });
            simulate.FrameUpdate();
            Assert.AreEqual(1, Bars(series).Length);
            Assert.IsTrue(Bars(series)[0].ClassListContains("bar-series__bar--empty"));

            series.SetValues(new float?[] { 0.5f, 1f });
            simulate.FrameUpdate();
            var bars = Bars(series);
            Assert.AreEqual(2, bars.Length);
            Assert.IsTrue(bars[0].ClassListContains("bar-series__bar--leading"));
            Assert.IsFalse(bars[1].ClassListContains("bar-series__bar--leading"));
            Assert.IsFalse(bars[0].ClassListContains("bar-series__bar--empty"));
            CollectionAssert.AreEqual(new[] { 50f, 100f }, FillPercents(series));
        }

        [Test]
        public void 溝と線と充填はGaugeBarと同じ色で充填が線を覆い入力を受けない()
        {
            var series = Mount(rootVisualElement, new float?[] { 0.5f });
            var bar = Bars(series)[0];
            var outline = bar.Q(className: "bar-series__bar-outline");
            var fill = bar.Q(className: "bar-series__bar-fill");

            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--gauge-track"), bar.resolvedStyle.backgroundColor, "track");
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--bevel-1"), bar.resolvedStyle.borderTopLeftRadius, 0.001f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--gauge-outline-width"), outline.resolvedStyle.borderTopWidth, 0.001f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--bevel-c1"), outline.resolvedStyle.borderTopColor, "outline");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--gauge-fill"), fill.resolvedStyle.backgroundColor, "fill");
            Assert.Less(bar.IndexOf(outline), bar.IndexOf(fill));
            series.Query<VisualElement>().ForEach(element => Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.GetClasses().FirstOrDefault()));
        }

        [Test]
        public void 祖先でつまみを上書きすると寸法と隙間と溝と充填の色が変わる()
        {
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            rootVisualElement.styleSheets.Add(probe);
            var screen = new VisualElement();
            screen.AddToClassList("gauge-knob-probe");
            rootVisualElement.Add(screen);
            var series = Mount(screen, new float?[] { 1f, 1f });

            var bars = Bars(series);
            Assert.AreEqual(40f, series.layout.height, 0.5f);
            Assert.AreEqual(6f, bars[0].layout.width, 0.5f);
            Assert.AreEqual(5f, bars[1].layout.x - bars[0].layout.xMax, 0.5f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--bevel-c1"), bars[0].Q(className: "bar-series__bar-fill").resolvedStyle.backgroundColor, "fill");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--color-content-primary"), bars[0].resolvedStyle.backgroundColor, "track");
        }

        private static VisualElement[] Bars(BarSeries series)
        {
            return series.Query(className: "bar-series__bar").ToList().ToArray();
        }

        private static float[] FillPercents(BarSeries series)
        {
            return series.Query(className: "bar-series__bar-fill").ToList().Select(fill => fill.style.height.value.value).ToArray();
        }

        private BarSeries Mount(VisualElement parent, float?[] values)
        {
            var series = new BarSeries("bars");
            series.SetValues(values);
            parent.Add(series);
            simulate.FrameUpdate();
            return series;
        }
    }
}
