using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// 本番テーマのフォントとフォールバックが日本語・韓国語・英数を描けることを確かめる（L1・S10）
    /// Verifies the production theme's fonts and fallbacks cover Japanese, Korean and Latin (L1, S10)
    /// </summary>
    public class GameUiFontTest : UiTestHost
    {
        private const string FontFolder = "Assets/UI Toolkit/GameUi/Fonts";

        [Test]
        public void LabelUsesLatinByDefaultAndJapaneseUnderCjkClass()
        {
            var label = new Label("x");
            rootVisualElement.Add(label);
            simulate.FrameUpdate();
            Assert.AreEqual(Load("GameUiLatin SDF"), label.resolvedStyle.unityFontDefinition.fontAsset);

            rootVisualElement.AddToClassList(GameUiLocaleClass.CjkClassName);
            simulate.FrameUpdate();
            Assert.AreEqual(Load("GameUiJapanese SDF"), label.resolvedStyle.unityFontDefinition.fontAsset);
        }

        [Test]
        public void IsCjkMatchesWebLocaleRuleForEveryCatalogLanguage()
        {
            // 言語カタログの全言語で、Web の :root[data-locale] と同じ 4 言語だけが CJK
            // Across the whole language catalog, only the Web's four :root[data-locale] languages are CJK
            var cjk = new System.Collections.Generic.HashSet<string> { "japanese", "korean", "chinese-simplified", "chinese-traditional" };
            foreach (var language in Mooresmaster.Localization.Generated.LanguageCatalog.Languages) Assert.AreEqual(cjk.Contains(language.Code), GameUiLocaleClass.IsCjk(language.Code), language.Code);
        }

        [TestCase("電力・鉄鉱石の粉・歯車ベルトコンベア")]
        [TestCase("설정 저장")]
        [TestCase("Settings 1,234.5/min")]
        public void FallbackChainCoversSample(string sample)
        {
            // Latin を先頭にしたフォールバックの連鎖のどこかで全字形が見つかる
            // Every glyph is found somewhere along the Latin-led fallback chain
            var latin = Load("GameUiLatin SDF");
            foreach (var character in sample)
            {
                if (char.IsWhiteSpace(character)) continue;
                Assert.IsTrue(HasInChain(latin, character), $"missing glyph '{character}' (U+{(int)character:X4})");
            }
        }

        [Test]
        public void 和文の題の字形は韓国語の字をKRのBoldで描く()
        {
            // Mac の Chrome の題（Apple SD Gothic Neo Bold）と太さを揃えるため、JP Bold に無い字は KR Bold から引く（設計書 A-220）
            // To match the Mac Chrome title weight (Apple SD Gothic Neo Bold), glyphs missing from JP Bold come from KR Bold (design A-220)
            var japaneseBold = Load("GameUiJapaneseBold SDF");
            Assert.IsFalse(japaneseBold.HasCharacter('설', false, true), "JP Bold has no Hangul");
            CollectionAssert.AreEqual(new[] { Load("GameUiKoreanBold SDF") }, japaneseBold.fallbackFontAssetTable);
            Assert.IsTrue(Load("GameUiKoreanBold SDF").HasCharacter('설', false, true));
        }

        private static bool HasInChain(FontAsset asset, char character)
        {
            if (asset.HasCharacter(character, false, true)) return true;
            foreach (var fallback in asset.fallbackFontAssetTable) if (HasInChain(fallback, character)) return true;
            return false;
        }

        private static FontAsset Load(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<FontAsset>($"{FontFolder}/{name}.asset");
            Assert.IsNotNull(asset, $"{FontFolder}/{name}.asset");
            return asset;
        }
    }
}
