using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.Typography
{
    /// <summary>
    /// Title・fw の太さと CJK 字形の確認
    /// Checks Title/fw weights and CJK faces
    /// </summary>
    public class UiTypographyWeightTest : UiTestHost
    {
        private const string Folder = "Assets/UI Toolkit/GameUi/Fonts";

        // Web は欧文を Regular の 1 字形だけ読み込むので、Title（700）と fw={700}・fw={500}・keyHintText も Regular で描く（設計書 A-220）
        // The Web loads only the Latin Regular face, so Title (700), fw={700}, fw={500} and keyHintText draw with Regular too (design A-220)
        [TestCase(UiTypographyClass.TitleOrder1)]
        [TestCase(UiTypographyClass.TitleOrder4)]
        [TestCase(UiTypographyClass.TextBold)]
        [TestCase(UiTypographyClass.TextMedium)]
        [TestCase(UiTypographyClass.KeyHintText)]
        public void 欧文のロケールでは太さのクラスに欧文のRegularを当てる(string className)
        {
            var label = Mount(rootVisualElement, className);
            Assert.AreEqual(Load("GameUiLatin SDF"), label.resolvedStyle.unityFontDefinition.fontAsset);
        }

        // Web は和文ロケールを NotoSansJP の Medium 1 字形で描くので、題・太字・500 も Medium（設計書 A-220）
        // The Web draws CJK locales with the single NotoSansJP Medium face, so titles, bold and 500 use Medium too (design A-220)
        [TestCase(UiTypographyClass.TitleOrder2)]
        [TestCase(UiTypographyClass.TitleOrder4)]
        [TestCase(UiTypographyClass.TextBold)]
        [TestCase(UiTypographyClass.TextMedium)]
        [TestCase(UiTypographyClass.KeyHintText)]
        public void CJKのロケールでは太さのクラスに日本語のMediumを当てる(string className)
        {
            var locale = new VisualElement();
            locale.AddToClassList(GameUiLocaleClass.CjkClassName);
            rootVisualElement.Add(locale);
            var label = Mount(locale, className);
            Assert.AreEqual(Load("GameUiJapanese SDF"), label.resolvedStyle.unityFontDefinition.fontAsset);
        }

        private Label Mount(VisualElement parent, string className)
        {
            var label = new Label("Weight");
            label.AddToClassList(className);
            parent.Add(label);
            simulate.FrameUpdate();
            return label;
        }

        private static FontAsset Load(string assetName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<FontAsset>($"{Folder}/{assetName}.asset");
            Assert.IsNotNull(asset, assetName);
            return asset;
        }
    }
}
