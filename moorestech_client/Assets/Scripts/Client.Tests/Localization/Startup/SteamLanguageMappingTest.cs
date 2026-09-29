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
            Assert.IsTrue(SteamLanguageMapping.TryToGameLanguage(steamLanguage, out var gameLanguage));
            Assert.AreEqual(expected, gameLanguage);
        }

        [TestCase("french")]
        [TestCase("")]
        public void FailsForUnmappedSteamLanguage(string steamLanguage)
        {
            Assert.IsFalse(SteamLanguageMapping.TryToGameLanguage(steamLanguage, out _));
        }
    }
}
