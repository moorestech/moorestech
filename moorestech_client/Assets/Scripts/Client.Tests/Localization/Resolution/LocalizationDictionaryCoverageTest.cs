using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizationDictionaryCoverageTest
    {
        [Test]
        public void LoadingKeysResolve()
        {
            Localize.Initialize();

            AssertResolves(LocalizationKeys.Ui.Loading.ServerConnected);
            AssertResolves(LocalizationKeys.Ui.Loading.InitialDataFetched);
            AssertResolves(LocalizationKeys.Ui.Loading.BlockAssetsLoaded);
            AssertResolves(LocalizationKeys.Ui.Loading.ItemImagesLoaded);
            AssertResolves(LocalizationKeys.Ui.Loading.ConnectToolImagesLoaded);
            AssertResolves(LocalizationKeys.Ui.Loading.FluidImagesLoaded);
            AssertResolves(LocalizationKeys.Ui.Loading.BlockScreenshotsCaptured);
            AssertResolves(LocalizationKeys.Ui.Loading.TrainCarScreenshotsCaptured);
            AssertResolves(LocalizationKeys.Ui.Loading.TerrainReady);
            AssertResolves(LocalizationKeys.Ui.Loading.InitializationFailed);
        }

        [Test]
        public void MainMenuConnectErrorKeysResolve()
        {
            Localize.Initialize();

            AssertResolves(LocalizationKeys.Ui.MainMenu.ConnectInvalidIp);
            AssertResolves(LocalizationKeys.Ui.MainMenu.ConnectPortTooLarge);
            AssertResolves(LocalizationKeys.Ui.MainMenu.ConnectPortTooSmall);
            AssertResolves(LocalizationKeys.Ui.MainMenu.ConnectFailed);
        }

        /// <web-test file="src/shared/i18n/allScreensI18n.test.ts">requires non-empty Source and every language cell for every generated key</web-test>
        [Test]
        public void EveryVanillaKeyHasNonEmptyTextInEveryLanguage()
        {
            Localize.Initialize();

            // フィルタ前のキー集合を正準とし空訳の取りこぼしを検出する
            // Use the pre-filter key set as canonical so dropped empty entries are still caught
            foreach (var language in LanguageCatalog.Languages)
            {
                var languageCode = language.Code;
                Assert.IsTrue(Localize.TryGetDictionary(languageCode, out var dictionary), languageCode);
                foreach (var key in VanillaLocalizationTable.SourceTexts.Keys)
                {
                    var hasKey = dictionary.TryGetValue(key, out var text);
                    Assert.IsTrue(hasKey, $"{languageCode}:{key}");
                    Assert.IsNotEmpty(text, $"{languageCode}:{key}");
                }
            }
        }

        /// <web-test file="src/shared/i18n/allScreensI18n.test.ts">requires non-empty Source and every language cell for every generated key</web-test>
        [Test]
        public void EveryVanillaKeyHasNonEmptySourceText()
        {
            // 生成キーの正準である Source の文が空のキーを残さない
            // No generated key may keep an empty Source text, the canonical column
            foreach (var pair in VanillaLocalizationTable.SourceTexts)
            {
                Assert.IsNotEmpty(pair.Value, pair.Key);
            }
        }

        // 欠落キーは[!key]マーカーで返るため、それを弾く
        // Missing keys come back as a [!key] marker, so reject those
        private static void AssertResolves(LocalizationKey key)
        {
            var text = Localize.Get(key);
            Assert.IsNotEmpty(text, key.Key);
            StringAssert.DoesNotStartWith("[!", text, key.Key);
        }
    }
}
