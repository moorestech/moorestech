using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// Title の字間の詰めが、Bold の題の幅を Web の Regular の題の幅へ寄せることを確かめる（設計書 A-37）
    /// Verifies the title letter-spacing brings the Bold title width to the Web's Regular title width (design A-37)
    /// </summary>
    public class UiTitleLetterSpacingTest : UiTestHost
    {
        private const string RegularFontPath = "Assets/UI Toolkit/GameUi/Fonts/GameUiLatin SDF.asset";
        private const string LatinTitleText = "Settings Language Graphics Inventory";

        // Web は Title を Regular の 1 字形で描くので、Regular・字間 0 の幅が Web の題の幅
        // The Web draws titles with its single Regular face, so the Regular width at zero spacing is the Web title width
        [TestCase(UiTypographyClass.TitleOrder1)]
        [TestCase(UiTypographyClass.TitleOrder2)]
        [TestCase(UiTypographyClass.TitleOrder3)]
        [TestCase(UiTypographyClass.TitleOrder4)]
        public void 欧文の題の幅はWebのRegularの題の幅と3パーセント以内(string className)
        {
            var title = Mount(rootVisualElement, className, LatinTitleText);
            var webWidthTitle = Mount(rootVisualElement, className, LatinTitleText);
            var regular = AssetDatabase.LoadAssetAtPath<FontAsset>(RegularFontPath);
            Assert.IsNotNull(regular, RegularFontPath);
            webWidthTitle.style.unityFontDefinition = FontDefinition.FromSDFFont(regular);
            webWidthTitle.style.letterSpacing = 0f;
            simulate.FrameUpdate();

            Assert.AreEqual(1f, title.layout.width / webWidthTitle.layout.width, 0.03f, $"{className}: {title.layout.width} vs {webWidthTitle.layout.width}");
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
