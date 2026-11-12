using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// Title の字間を Web と同じく詰めないか
    /// Checks titles keep the Web's untightened letter spacing
    /// </summary>
    public class UiTitleLetterSpacingTest : UiTestHost
    {
        private const string LatinTitleText = "Settings Language Graphics Inventory";

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
