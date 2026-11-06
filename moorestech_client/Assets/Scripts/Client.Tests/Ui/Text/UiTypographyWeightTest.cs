using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// Title と fw のクラスが Web と同じ太さの字形（700・500）を使い、CJK のロケールでは JP の字形を先に使うことを確かめる
    /// Verifies Title and fw classes use the Web's weights (700, 500) and that CJK locales lead with the JP face
    /// </summary>
    public class UiTypographyWeightTest : UiTestHost
    {
        private const string Folder = "Assets/UI Toolkit/GameUi/Fonts";

        [TestCase(UiTypographyClass.TitleOrder1, "GameUiLatinBold SDF")]
        [TestCase(UiTypographyClass.TitleOrder4, "GameUiLatinBold SDF")]
        [TestCase("text--bold", "GameUiLatinBold SDF")]
        [TestCase("text--medium", "GameUiLatinMedium SDF")]
        [TestCase(UiTypographyClass.KeyHintText, "GameUiLatinMedium SDF")]
        public void 太さのクラスはWebの太さの字形を当てる(string className, string assetName)
        {
            var label = Mount(rootVisualElement, className);
            Assert.AreEqual(Load(assetName), label.resolvedStyle.unityFontDefinition.fontAsset);
        }

        [Test]
        public void CJKのロケールでは題に日本語の太字を先に当てる()
        {
            var locale = new VisualElement();
            locale.AddToClassList("game-ui-locale--cjk");
            rootVisualElement.Add(locale);
            var label = Mount(locale, UiTypographyClass.TitleOrder2);
            Assert.AreEqual(Load("GameUiJapaneseBold SDF"), label.resolvedStyle.unityFontDefinition.fontAsset);
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
