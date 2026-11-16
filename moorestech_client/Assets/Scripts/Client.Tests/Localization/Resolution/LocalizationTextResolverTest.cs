using System.Collections.Generic;
using Client.Localization;
using NUnit.Framework;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizationTextResolverTest
    {
        /// <web-test file="src/shared/i18n/useI18n.test.ts">uses the fallback locale when the current dictionary lacks a key</web-test>
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

        /// <web-test file="src/shared/i18n/useI18n.test.ts">uses the source text when the current and fallback dictionaries lack a key</web-test>
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

        /// <web-test file="src/shared/i18n/useI18n.test.ts">treats empty current and fallback translations as missing and uses source</web-test>
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

        /// <web-test file="src/shared/i18n/useI18n.test.ts">current locale wins and interpolates named values</web-test>
        [Test]
        public void CurrentTextWinsAndInterpolatesNamedValues()
        {
            var snapshot = CreateSnapshot(
                new Dictionary<string, string> { { "test.key", "こんにちは、{name}。残り{count}個" } },
                new Dictionary<string, string> { { "test.key", "Hello, {name}" } },
                new Dictionary<string, string> { { "test.key", "Source {name}" } });

            // 対象言語の文言を選び、名前付きの値で埋める
            // Pick the target-language text and fill its named values
            var template = LocalizationTextResolver.Resolve(snapshot, "japanese", "test.key");
            var text = LocalizationTextInterpolator.Interpolate("test.key", template, new[]
            {
                new LocalizationTextParam("name", "Moore"),
                new LocalizationTextParam("count", "3"),
            });

            Assert.AreEqual("こんにちは、Moore。残り3個", text);
        }

        /// <web-test file="src/shared/i18n/useI18n.test.ts">treats an empty current translation as missing and uses fallback</web-test>
        [Test]
        public void EmptyTargetTextFallsBackToEnglish()
        {
            var snapshot = CreateSnapshot(
                new Dictionary<string, string> { { "test.key", "" } },
                new Dictionary<string, string> { { "test.key", "English" } },
                new Dictionary<string, string> { { "test.key", "Source" } });

            var result = LocalizationTextResolver.Resolve(snapshot, "japanese", "test.key");

            Assert.AreEqual("English", result);
        }

        /// <web-test file="src/shared/i18n/useI18n.test.ts">shows a loud marker and warns when all three translation layers are empty</web-test>
        [Test]
        public void EmptyAllFallbackTextsReturnsMarker()
        {
            var snapshot = CreateSnapshot(
                new Dictionary<string, string> { { "test.key", "" } },
                new Dictionary<string, string> { { "test.key", "" } },
                new Dictionary<string, string> { { "test.key", "" } });

            var result = LocalizationTextResolver.Resolve(snapshot, "japanese", "test.key");

            Assert.AreEqual("[!test.key]", result);
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
