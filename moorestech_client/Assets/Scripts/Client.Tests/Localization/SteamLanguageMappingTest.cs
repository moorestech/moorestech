using Client.Localization;
using Client.Starter.Localization;
using NUnit.Framework;

namespace Client.Tests.Localization
{
    public class SteamLanguageMappingTest
    {
        [TestCase("english", "english")]
        [TestCase("japanese", "japanese")]
        [TestCase("german", "german")]
        public void MapsSteamLanguageListedInCatalog(string steamLanguage, string expected)
        {
            Assert.AreEqual(expected, SteamLanguageMapping.ToGameLanguage(steamLanguage));
        }

        [TestCase("french")]
        [TestCase("")]
        public void FallsBackToEnglishForUnmappedSteamLanguage(string steamLanguage)
        {
            Assert.AreEqual(Localize.DefaultLanguageCode, SteamLanguageMapping.ToGameLanguage(steamLanguage));
        }
    }
}
