using System.Collections.Generic;
using Client.Localization;
using NUnit.Framework;

namespace Client.Tests.Localization
{
    public class LocalizationTextResolverTest
    {
        [Test]
        public void MissingTargetTextFallsBackToEnglish()
        {
            var dictionaries = CreateDictionaries(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { { "test.key", "English" } },
                new Dictionary<string, string> { { "test.key", "Source" } });

            var result = LocalizationTextResolver.Resolve(dictionaries, "japanese", "test.key");

            Assert.AreEqual("English", result);
        }

        [Test]
        public void MissingTargetAndEnglishTextFallsBackToSource()
        {
            var dictionaries = CreateDictionaries(
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string> { { "test.key", "Source" } });

            var result = LocalizationTextResolver.Resolve(dictionaries, "japanese", "test.key");

            Assert.AreEqual("Source", result);
        }

        [Test]
        public void MissingAllFallbackTextsReturnsMarker()
        {
            var dictionaries = CreateDictionaries(
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>());

            var result = LocalizationTextResolver.Resolve(dictionaries, "japanese", "test.key");

            Assert.AreEqual("[!test.key]", result);
        }

        [Test]
        public void EmptyTargetAndEnglishTextsFallBackToSource()
        {
            var dictionaries = CreateDictionaries(
                new Dictionary<string, string> { { "test.key", "" } },
                new Dictionary<string, string> { { "test.key", "" } },
                new Dictionary<string, string> { { "test.key", "Source" } });

            var result = LocalizationTextResolver.Resolve(dictionaries, "japanese", "test.key");

            Assert.AreEqual("Source", result);
        }

        private static Dictionary<string, IReadOnlyDictionary<string, string>> CreateDictionaries(
            IReadOnlyDictionary<string, string> target,
            IReadOnlyDictionary<string, string> english,
            IReadOnlyDictionary<string, string> source)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                { "japanese", target },
                { "english", english },
                { "source", source },
            };
        }
    }
}
