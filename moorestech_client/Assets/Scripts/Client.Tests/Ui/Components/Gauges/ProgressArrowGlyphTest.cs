using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Gauges;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Gauges
{
    /// <summary>
    /// ProgressArrowGlyph の充填の切り抜き・層の並び・静止表示・寸法を Web の shared/ui/ProgressArrowGlyph と照らす
    /// Checks the fill clip, layer order, static form and size of ProgressArrowGlyph against Web shared/ui/ProgressArrowGlyph
    /// </summary>
    public class ProgressArrowGlyphTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/Gauges/GaugeKnobOverrides.uss";
        private const string IdleClassName = "progress-arrow-glyph--idle";

        // UiTestHost は倍率 1 でレイアウトを画素へ丸める（前例 SlotFrameLookTest「51.68 が 52」）。トークンとの比較は 0.5px、丸めた箱から導いた子は 1px まで許す
        // UiTestHost rounds layout to pixels at scale 1 (precedent SlotFrameLookTest, 51.68 becomes 52); allow 0.5px against tokens and 1px for children derived from the rounded box
        private const float TokenPixelTolerance = 0.5f;
        private const float DerivedPixelTolerance = 1f;

        /// <web-test file="src/shared/ui/ProgressArrowGlyph/index.test.ts">value=$valueを充填幅$widthへクランプする</web-test>
        [TestCase(-0.2f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.5f, 58.5f)]
        [TestCase(1f, 117f)]
        [TestCase(1.4f, 117f)]
        [TestCase(float.NaN, 0f)]
        public void 値を充填の切り抜きの幅へ丸める(float value, float expectedViewBoxWidth)
        {
            var glyph = Mount(rootVisualElement);
            glyph.SetProgress(value);
            simulate.FrameUpdate();

            // Web の clipPath の rect の幅（viewBox の単位）を、箱の幅に対する % で持つ
            // The Web clipPath rect width (viewBox units) is held as a percent of the box width
            var clip = glyph.Q(className: "progress-arrow-glyph__fill-clip");
            Assert.IsFalse(glyph.ClassListContains(IdleClassName));
            Assert.AreEqual(LengthUnit.Percent, clip.style.width.value.unit);
            Assert.AreEqual(expectedViewBoxWidth / 121f * 100f, clip.style.width.value.value, 0.001f);
            Assert.AreEqual(expectedViewBoxWidth * ScaleOf(glyph), clip.layout.width, DerivedPixelTolerance);
        }

        /// <web-test file="src/shared/ui/ProgressArrowGlyph/index.test.ts">クリップ矩形が矢印pathの全域を覆う</web-test>
        [Test]
        public void 満杯の切り抜きは矢印の水平範囲と縦の全域を覆い充填の面はviewBoxにそろう()
        {
            var glyph = Mount(rootVisualElement);
            glyph.SetProgress(1f);
            simulate.FrameUpdate();

            var scale = ScaleOf(glyph);
            var viewBox = glyph.Q(className: "progress-arrow-glyph__view-box");
            var clip = glyph.Q(className: "progress-arrow-glyph__fill-clip");
            var fill = glyph.Q(className: "progress-arrow-glyph__fill");
            Assert.AreEqual(2f * scale, clip.layout.x, DerivedPixelTolerance);
            Assert.AreEqual(117f * scale, clip.layout.width, DerivedPixelTolerance);
            Assert.AreEqual(0f, clip.layout.y, DerivedPixelTolerance);
            Assert.AreEqual(viewBox.layout.height, clip.layout.height, DerivedPixelTolerance);
            Assert.AreEqual(0f, clip.layout.x + fill.layout.x, DerivedPixelTolerance);
            Assert.AreEqual(viewBox.layout.width, fill.layout.width, DerivedPixelTolerance);
        }

        /// <web-test file="src/shared/ui/ProgressArrowGlyph/index.test.ts">clipは充填層だけに付き、輪郭は最上層でclipされない</web-test>
        [Test]
        public void 切り抜きは充填だけを包み輪郭は切り抜きの外の最上層()
        {
            var glyph = Mount(rootVisualElement);
            glyph.SetProgress(0.5f);
            simulate.FrameUpdate();

            var viewBox = glyph.Q(className: "progress-arrow-glyph__view-box");
            Assert.AreEqual(4, viewBox.childCount);
            Assert.IsTrue(viewBox[0].ClassListContains("progress-arrow-glyph__shadow"));
            Assert.IsTrue(viewBox[1].ClassListContains("progress-arrow-glyph__track"));
            Assert.IsTrue(viewBox[2].ClassListContains("progress-arrow-glyph__fill-clip"));
            Assert.IsTrue(viewBox[3].ClassListContains("progress-arrow-glyph__outline"));
            Assert.AreEqual(1, viewBox[2].childCount);
            Assert.IsTrue(viewBox[2][0].ClassListContains("progress-arrow-glyph__fill"));
        }

        /// <web-test file="src/shared/ui/ProgressArrowGlyph/index.test.ts">value=nullではprogressbarロールもaria値も出さない</web-test>
        [Test]
        public void 作った直後は進捗を名乗らない静止の印を持ち名前はtestid()
        {
            // UITK に ARIA は無いので、Web の progressbar ロールの有無を静止の印の有無で写す
            // UITK has no ARIA, so the Web progressbar role maps to the absence of the idle marker
            var glyph = Mount(rootVisualElement);

            Assert.AreEqual("arrow", glyph.name);
            Assert.IsTrue(glyph.ClassListContains(IdleClassName));
        }

        /// <web-test file="src/shared/ui/ProgressArrowGlyph/index.test.ts">value=nullでは充填層とclipPathを描かない</web-test>
        [Test]
        public void 静止では切り抜きごと充填を隠し溝と輪郭だけを出す()
        {
            var glyph = Mount(rootVisualElement);
            glyph.SetProgress(0.5f);
            glyph.SetIdle();
            simulate.FrameUpdate();

            Assert.IsTrue(glyph.ClassListContains(IdleClassName));
            Assert.AreEqual(DisplayStyle.None, glyph.Q(className: "progress-arrow-glyph__fill-clip").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, glyph.Q(className: "progress-arrow-glyph__track").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, glyph.Q(className: "progress-arrow-glyph__outline").resolvedStyle.display);
        }

        /// <web-test file="src/shared/ui/ProgressArrowGlyph/index.test.ts">同一ページに複数並んでもclip idが衝突しない</web-test>
        [Test]
        public void 並べた2つの矢印はそれぞれの進捗で切り抜く()
        {
            var first = Mount(rootVisualElement);
            var second = Mount(rootVisualElement);
            first.SetProgress(0.25f);
            second.SetProgress(0.75f);
            simulate.FrameUpdate();

            // 並べた位置で丸めが変わり高さが 1px 違いうるので、倍率は矢印ごとに取る
            // Rounding depends on position and heights can differ by 1px, so take each arrow's own scale
            Assert.AreEqual(117f * 0.25f * ScaleOf(first), first.Q(className: "progress-arrow-glyph__fill-clip").layout.width, DerivedPixelTolerance);
            Assert.AreEqual(117f * 0.75f * ScaleOf(second), second.Q(className: "progress-arrow-glyph__fill-clip").layout.width, DerivedPixelTolerance);
        }

        [Test]
        public void 寸法はクラフト矢印のトークンでviewBoxを中央に当てはめ入力を受けない()
        {
            var glyph = Mount(rootVisualElement);

            // 箱の期待値は丸めた後の contentRect から当てはめ直し、端数は丸め前の style で一致を見る
            // Box expectations refit from the rounded contentRect; fractions are matched on the unrounded style
            var viewBox = glyph.Q(className: "progress-arrow-glyph__view-box");
            var box = ProgressArrowGlyphPath.FitViewBox(glyph.contentRect.size);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--craft-arrow-width"), glyph.layout.width, TokenPixelTolerance);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--craft-arrow-height"), glyph.layout.height, TokenPixelTolerance);
            Assert.AreEqual(box.width, viewBox.style.width.value.value, 1e-3f);
            Assert.AreEqual(box.x, viewBox.style.left.value.value, 1e-3f);
            Assert.AreEqual(box.width, viewBox.layout.width, DerivedPixelTolerance);
            Assert.AreEqual(box.height, viewBox.layout.height, DerivedPixelTolerance);
            Assert.AreEqual(box.x, viewBox.layout.x, DerivedPixelTolerance);
            glyph.Query<VisualElement>().ForEach(element => Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name));
        }

        [Test]
        public void 祖先で寸法のつまみを上書きするとviewBoxと切り抜きが同じ倍率で拡がる()
        {
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            rootVisualElement.styleSheets.Add(probe);
            var screen = new VisualElement();
            screen.AddToClassList("gauge-knob-probe");
            rootVisualElement.Add(screen);
            var glyph = Mount(screen);
            glyph.SetProgress(1f);
            simulate.FrameUpdate();

            // 寸法はつまみの値（丸めで 0.5px まで）、viewBox と切り抜きは丸めた箱の倍率で拡がる
            // The size follows the knobs (0.5px rounding); viewBox and clip scale with the rounded box
            var scale = ScaleOf(glyph);
            Assert.AreEqual(107.244f, glyph.layout.width, TokenPixelTolerance);
            Assert.AreEqual(61.06f, glyph.layout.height, TokenPixelTolerance);
            Assert.AreEqual(61.06f / 78f, scale, 1f / 78f);
            Assert.AreEqual(121f * scale, glyph.Q(className: "progress-arrow-glyph__view-box").layout.width, DerivedPixelTolerance);
            Assert.AreEqual(117f * scale, glyph.Q(className: "progress-arrow-glyph__fill-clip").layout.width, DerivedPixelTolerance);
            Assert.AreEqual(-2f * scale, glyph.Q(className: "progress-arrow-glyph__fill").layout.x, DerivedPixelTolerance);
        }

        [Test]
        public void 進捗0は静止と違い幅0の切り抜きを出す()
        {
            var glyph = Mount(rootVisualElement);
            glyph.SetProgress(0f);
            simulate.FrameUpdate();

            var clip = glyph.Q(className: "progress-arrow-glyph__fill-clip");
            Assert.IsFalse(glyph.ClassListContains(IdleClassName));
            Assert.AreEqual(DisplayStyle.Flex, clip.resolvedStyle.display);
            Assert.AreEqual(0f, clip.layout.width, 0.05f);
        }

        private static float ScaleOf(ProgressArrowGlyph glyph)
        {
            return ProgressArrowGlyphPath.ScaleOf(glyph.contentRect.size);
        }

        private ProgressArrowGlyph Mount(VisualElement parent)
        {
            // 1 回目の更新で寸法が決まり、GeometryChangedEvent で当てた viewBox の箱は 2 回目で並ぶ
            // The first update sizes the root; the viewBox placed on GeometryChangedEvent lays out on the second
            var glyph = new ProgressArrowGlyph("arrow");
            parent.Add(glyph);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return glyph;
        }
    }
}
