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
            var snapshot = CreateSnapshot(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { { "test.key", "English" } },
                new Dictionary<string, string> { { "test.key", "Source" } });

            var result = LocalizationTextResolver.Resolve(snapshot, "japanese", "test.key");

            Assert.AreEqual("English", result);
        }

        [Test]
        public void MissingTargetAndEnglishTextFallsBackToSource()
        {
            var snapshot = CreateSnapshot(
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string> { { "test.key", "Source" } });

            var result = LocalizationTextResolver.Resolve(snapshot, "japanese", "test.key");

            Assert.AreEqual("Source", result);
        }

        [Test]
        public void MissingAllFallbackTextsReturnsMarker()
        {
            var snapshot = CreateSnapshot(
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>());

            var result = LocalizationTextResolver.Resolve(snapshot, "japanese", "test.key");

            Assert.AreEqual("[!test.key]", result);
        }

        [Test]
        public void EmptyTargetAndEnglishTextsFallBackToSource()
        {
            var snapshot = CreateSnapshot(
                new Dictionary<string, string> { { "test.key", "" } },
                new Dictionary<string, string> { { "test.key", "" } },
                new Dictionary<string, string> { { "test.key", "Source" } });

            var result = LocalizationTextResolver.Resolve(snapshot, "japanese", "test.key");

            Assert.AreEqual("Source", result);
        }

        private static PublishedLocalizationDictionarySnapshot CreateSnapshot(
            IReadOnlyDictionary<string, string> target,
            IReadOnlyDictionary<string, string> english,
            IReadOnlyDictionary<string, string> source)
        {
            return new PublishedLocalizationDictionarySnapshot(
                1,
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    { "japanese", target },
                    { "english", english },
                },
                source);
        }
    }
}
