using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// Title・fw の太さと CJK 字形の確認
    /// Checks Title/fw weights and CJK faces
    /// </summary>
    public class UiTypographyWeightTest : UiTestHost
    {
        private const string Folder = "Assets/UI Toolkit/GameUi/Fonts";

        // Web は欧文を Regular の 1 字形だけ読み込むので、Title（700）と fw={700} も Regular で描く（設計書 A-220）
        // The Web loads only the Latin Regular face, so Title (700) and fw={700} draw with Regular too (design A-220)
        [TestCase(UiTypographyClass.TitleOrder1, "GameUiLatin SDF")]
        [TestCase(UiTypographyClass.TitleOrder4, "GameUiLatin SDF")]
        [TestCase(UiTypographyClass.TextBold, "GameUiLatin SDF")]
        [TestCase(UiTypographyClass.TextMedium, "GameUiLatinMedium SDF")]
        [TestCase(UiTypographyClass.KeyHintText, "GameUiLatinMedium SDF")]
        public void 太さのクラスはWebの太さの字形を当てる(string className, string assetName)
        {
            var label = Mount(rootVisualElement, className);
            Assert.AreEqual(Load(assetName), label.resolvedStyle.unityFontDefinition.fontAsset);
        }

        // Web は和文ロケールを NotoSansJP の Medium 1 字形で描くので、題と太字も Medium（設計書 A-220）
        // The Web draws CJK locales with the single NotoSansJP Medium face, so titles and bold use Medium too (design A-220)
        [TestCase(UiTypographyClass.TitleOrder2)]
        [TestCase(UiTypographyClass.TitleOrder4)]
        [TestCase(UiTypographyClass.TextBold)]
        public void CJKのロケールでは題と太字に日本語のMediumを当てる(string className)
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
