using System.Collections.Generic;
using Client.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

            LogAssert.Expect(LogType.Warning, "[Localize] missing text in every layer: test.key");
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

            LogAssert.Expect(LogType.Warning, "[Localize] missing text in every layer: test.key");
            var result = LocalizationTextResolver.Resolve(snapshot, "japanese", "test.key");

            Assert.AreEqual("[!test.key]", result);
        }

        /// <web-test file="src/shared/i18n/useI18n.test.ts">warns once per generation for an unknown external localized key</web-test>
        [Test]
        public void EveryLayerMissingWarnsOncePerRevisionAndKey()
        {
            // 自前の snapshot だけを引くので、他テストの報告済みに左右されない
            // Only this test's own snapshots are used, so other tests' reports do not interfere
            var empty = new Dictionary<string, string>();
            var revision1 = CreateSnapshot(empty, empty, empty);
            var revision2 = CreateSnapshot(empty, empty, empty);
            const string warning = "[Localize] missing text in every layer: test.missing";
            // LogAssert は予期しない警告では落ちないので、回数は受信の購読で数える
            // LogAssert does not fail on unexpected warnings, so count them via the log subscription
            var warnings = 0;
            Application.logMessageReceived += CountWarning;

            // 同版は警告 1 回、新版は再警告
            // Same revision warns once; a new revision warns again
            LogAssert.Expect(LogType.Warning, warning);
            LocalizationTextResolver.Resolve(revision1, "japanese", "test.missing");
            LocalizationTextResolver.Resolve(revision1, "japanese", "test.missing");
            var afterSameRevision = warnings;
            LogAssert.Expect(LogType.Warning, warning);
            LocalizationTextResolver.Resolve(revision2, "japanese", "test.missing");
            Application.logMessageReceived -= CountWarning;
            LogAssert.NoUnexpectedReceived();

            Assert.AreEqual(1, afterSameRevision, "同じ版×キーで警告が 1 回でない");
            Assert.AreEqual(2, warnings, "版が上がっても同じキーの警告が出直さない");

            #region Internal

            void CountWarning(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && condition == warning) warnings++;
            }

            #endregion
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
