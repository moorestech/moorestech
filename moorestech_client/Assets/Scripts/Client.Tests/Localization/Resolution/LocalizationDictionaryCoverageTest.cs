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

        [Test]
        public void EveryVanillaKeyHasNonEmptyTextInEveryLanguage()
        {
            Localize.Initialize();

            foreach (var languageCode in Localize.GetLanguageCodes())
            {
                Assert.IsTrue(Localize.TryGetDictionary(languageCode, out var dictionary), languageCode);
                foreach (var pair in dictionary)
                {
                    Assert.IsNotEmpty(pair.Value, $"{languageCode}:{pair.Key}");
                }
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
