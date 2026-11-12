using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// Title の段だけの題は字間を詰めず、見出しの字形の題は Web の字送りへ詰めるか
    /// Checks plain title orders keep untightened spacing and heading-face titles match the Web advance
    /// </summary>
    public class UiTitleLetterSpacingTest : UiTestHost
    {
        private const string LatinTitleText = "Settings Language Graphics Inventory";
        private const string GapSample = "IIIIIIIIII";
        private const int GapCount = 9;

        // 題は Web と同じ Regular の字形なので字間を詰めない（設計書 A-220 で A-37 の -3.6 を外した）
        // Titles use the Web's Regular face, so their spacing is not tightened (design A-220 dropped A-37's -3.6)
        [TestCase(UiTypographyClass.TitleOrder1)]
        [TestCase(UiTypographyClass.TitleOrder2)]
        [TestCase(UiTypographyClass.TitleOrder3)]
        [TestCase(UiTypographyClass.TitleOrder4)]
        public void 欧文の題の字間を詰めない(string className)
        {
            var title = Mount(rootVisualElement, className, LatinTitleText);

            Assert.AreEqual(0f, title.resolvedStyle.letterSpacing, 0.001f);
        }

        // 和文は全角 1 字の字送りで太さによらず幅が同じなので、CJK のロケールでは詰めない
        // CJK glyphs advance one full em regardless of weight, so CJK locales do not tighten titles
        [TestCase(UiTypographyClass.TitleOrder1)]
        [TestCase(UiTypographyClass.TitleOrder4)]
        public void CJKのロケールでは題の字間を詰めない(string className)
        {
            var locale = new VisualElement();
            locale.AddToClassList(GameUiLocaleClass.CjkClassName);
            rootVisualElement.Add(locale);
            var title = Mount(locale, className, "設定");

            Assert.AreEqual(0f, title.resolvedStyle.letterSpacing, 0.001f);
        }

        // Mantine の Title は OS の字体の 700 で描くので、欧文は Bold の字形を SF Pro の 700 の字送りへ詰める（設計書 A-220。値は Mac の Chrome の実測）
        // Mantine Title draws with the OS face at 700, so Latin uses the Bold face tightened to SF Pro 700's advance (design A-220; values measured on Mac Chrome)
        [TestCase(UiTypographyClass.TitleOrder1, 0.034f)]
        [TestCase(UiTypographyClass.TitleOrder2, 0.032f)]
        [TestCase(UiTypographyClass.TitleOrder3, 0.033f)]
        [TestCase(UiTypographyClass.TitleOrder4, 0.020f)]
        public void 欧文のTitleは見出しの字形をWebの字送りへ詰める(string className, float webTightenEm)
        {
            var (spaced, unspaced) = MountHeadingPair(rootVisualElement, className, GapSample);

            Assert.AreEqual("GameUiLatinBold SDF", spaced.resolvedStyle.unityFontDefinition.fontAsset.name);
            ExpectTightenedByGaps(spaced, unspaced, webTightenEm);
        }

        // 和文は NotoSansJP Bold を 0.925 倍の大きさで字間を詰めずに描く（.Hiragino Kaku Gothic Interface W6 の字の面と字送り 0.925em）
        // CJK draws NotoSansJP Bold at 0.925x the size without tightening (.Hiragino Kaku Gothic Interface W6's glyph face and 0.925em advance)
        [TestCase(UiTypographyClass.TitleOrder1, 34f)]
        [TestCase(UiTypographyClass.TitleOrder2, 26f)]
        [TestCase(UiTypographyClass.TitleOrder3, 22f)]
        [TestCase(UiTypographyClass.TitleOrder4, 18f)]
        public void CJKのTitleは見出しのBoldを字送り0_925emの大きさで描く(string className, float orderFontSize)
        {
            var locale = new VisualElement();
            locale.AddToClassList(GameUiLocaleClass.CjkClassName);
            rootVisualElement.Add(locale);
            var heading = Mount(locale, className, "一一一一一一一一一一");
            heading.AddToClassList(UiTypographyClass.TitleHeadingFace);
            simulate.FrameUpdate();

            Assert.AreEqual("GameUiJapaneseBold SDF", heading.resolvedStyle.unityFontDefinition.fontAsset.name);
            Assert.AreEqual(orderFontSize * 0.925f, heading.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(0f, heading.resolvedStyle.letterSpacing, 0.001f);
        }

        private (Label spaced, Label unspaced) MountHeadingPair(VisualElement parent, string className, string text)
        {
            var spaced = Mount(parent, className, text);
            var unspaced = Mount(parent, className, text);
            spaced.AddToClassList(UiTypographyClass.TitleHeadingFace);
            unspaced.AddToClassList(UiTypographyClass.TitleHeadingFace);
            unspaced.style.letterSpacing = 0f;
            simulate.FrameUpdate();
            return (spaced, unspaced);
        }

        // UITK は字の間 9 か所に字間を足す
        // UITK adds the spacing at the 9 gaps between the 10 glyphs
        private static void ExpectTightenedByGaps(Label spaced, Label unspaced, float webTightenEm)
        {
            var expected = webTightenEm * spaced.resolvedStyle.fontSize * GapCount;
            Assert.AreEqual(expected, unspaced.layout.width - spaced.layout.width, 1f, $"{spaced.layout.width} vs {unspaced.layout.width}");
        }

        private Label Mount(VisualElement parent, string className, string text)
        {
            // 親の幅に引き伸ばされないよう絶対配置で字の幅だけにする
            // Absolute positioning keeps the label at its text width instead of stretching to the parent
            var label = new Label(text);
            label.AddToClassList(className);
            label.style.position = Position.Absolute;
            parent.Add(label);
            simulate.FrameUpdate();
            return label;
        }
    }
}
