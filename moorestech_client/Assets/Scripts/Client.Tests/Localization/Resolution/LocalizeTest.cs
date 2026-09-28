using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizeTest
    {
        private bool hadSavedLanguageCode;
        private string savedLanguageCode;

        [SetUp]
        public void SetUp()
        {
            // 各テスト前の保存値を正確に退避する
            // Preserve the exact persisted value before every test
            hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
        }

        [TearDown]
        public void TearDown()
        {
            // 保存値と有無を復元後、状態初期化
            // Restore value and presence, then reset state
            if (hadSavedLanguageCode)
            {
                PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            }
            else
            {
                PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            }

            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [TestCase("obsolete")]
        [TestCase(Localize.SourcePseudoLocale)]
        public void InitializeFallsBackToEnglishWhenSavedLanguageCannotBeSelected(string persistedLanguageCode)
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, persistedLanguageCode);
            LogAssert.Expect(LogType.Warning,
                $"[Localize] saved language {persistedLanguageCode} is unavailable; using english");
            Localize.Initialize();

            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void TypedKeyReturnsTextForSelectedEnglishAndJapaneseLanguages()
        {
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            var english = Localize.Get(LocalizationKeys.Ui.MainMenu.PlayLocally);

            Localize.TrySetChosenLanguage("japanese");
            var japanese = Localize.Get(LocalizationKeys.Ui.MainMenu.PlayLocally);

            Assert.AreEqual("Play locally", english);
            Assert.AreEqual("ローカルでプレイ", japanese);
        }

        [Test]
        public void BlueprintCopyTypedKeyReturnsTextForSelectedEnglishAndJapaneseLanguages()
        {
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            var english = Localize.Get(LocalizationKeys.Ui.BuildMenu.BlueprintCopy);

            Localize.TrySetChosenLanguage("japanese");
            var japanese = Localize.Get(LocalizationKeys.Ui.BuildMenu.BlueprintCopy);

            Assert.AreEqual("Blueprint Copy", english);
            Assert.AreEqual("ブループリントコピー", japanese);
        }

        [Test]
        public void InitializeCanRunTwiceWithoutChangingResolvedText()
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, Localize.DefaultLanguageCode);
            Localize.Initialize();
            Localize.Initialize();

            Assert.AreEqual("Play locally", Localize.Get(LocalizationKeys.Ui.MainMenu.PlayLocally));
        }

        [Test]
        public void GetLegacyReturnsMissingMarkerForUnknownRawKey()
        {
            Localize.Initialize();

            Assert.AreEqual("[!missing.raw.key]", Localize.GetLegacy("missing.raw.key"));
        }

        [Test]
        public void LanguageCatalogExcludesSourcePseudoLocale()
        {
            Localize.Initialize();

            var languageCodes = LanguageCatalog.Languages.Select(language => language.Code).ToArray();
            CollectionAssert.AreEqual(new[] { "english", "japanese", "german" }, languageCodes);
            CollectionAssert.DoesNotContain(languageCodes, Localize.SourcePseudoLocale);
        }

        [Test]
        public void TryGetDictionaryRejectsUnknownLanguage()
        {
            Localize.Initialize();

            var foundUnknown = Localize.TryGetDictionary("unknown", out var unknown);

            Assert.IsFalse(foundUnknown);
            Assert.IsNull(unknown);
        }

        [Test]
        public void SourcePseudoLocaleIsNotReachableThroughLanguageDictionaries()
        {
            Localize.Initialize();

            // 実言語辞書は除外条件ではなく型でSourceを持たない
            // Language dictionaries exclude Source by type, not by a runtime condition
            var foundSource = Localize.TryGetDictionary(Localize.SourcePseudoLocale, out var source);

            Assert.IsFalse(foundSource);
            Assert.IsNull(source);
        }

        [Test]
        public void TryGetSourceTextsReturnsSourceTextsForTheCurrentRevision()
        {
            Localize.Initialize();

            var found = Localize.TryGetSourceTexts(Localize.GetDictionaryRevision(), out var sourceTexts);

            Assert.IsTrue(found);
            Assert.AreEqual("Play locally", sourceTexts["ui.mainMenu.playLocally"]);
        }

        [Test]
        public void TryGetSourceTextsRejectsStaleRevision()
        {
            Localize.Initialize();
            var staleRevision = Localize.GetDictionaryRevision();
            Localize.Initialize();

            Assert.IsFalse(Localize.TryGetSourceTexts(staleRevision, out var sourceTexts));
            Assert.IsNull(sourceTexts);
        }

        [Test]
        public void TryGetDictionaryReturnsReadOnlyDictionary()
        {
            Localize.Initialize();

            var found = Localize.TryGetDictionary("english", out var dictionary);

            Assert.IsTrue(found);
            Assert.IsInstanceOf<ReadOnlyDictionary<string, string>>(dictionary);
            Assert.IsTrue(((ICollection<KeyValuePair<string, string>>)dictionary).IsReadOnly);
        }

        [Test]
        public void EmptyMasterSourceRemovesModSourceWithoutRemovingTargetOrEnglish()
        {
            var candidate = VanillaLocalizationDictionaryFactory.Create();
            ModLocalizationMerger.MergeCsv(
                "key,Source,english,japanese\n" +
                "content.empty.english,Wrong Source,English,\n" +
                "content.empty.marker,Wrong Source,,\n" +
                "content.empty.target,Wrong Source,English,対象\n",
                candidate);
            var masterSources = new Dictionary<string, string>
            {
                { "content.empty.english", "" },
                { "content.empty.marker", "" },
                { "content.empty.target", "" },
            };

            MasterSourceTextOverlay.Apply(candidate, masterSources);
            var snapshot = VanillaLocalizationDictionaryFactory.Freeze(candidate, 1);

            Assert.IsFalse(candidate.SourceTexts.ContainsKey("content.empty.english"));
            Assert.IsFalse(candidate.SourceTexts.ContainsKey("content.empty.marker"));
            Assert.IsFalse(candidate.SourceTexts.ContainsKey("content.empty.target"));
            Assert.AreEqual("English", LocalizationTextResolver.Resolve(snapshot, "japanese", "content.empty.english"));
            Assert.AreEqual("[!content.empty.marker]", LocalizationTextResolver.Resolve(snapshot, "japanese", "content.empty.marker"));
            Assert.AreEqual("対象", LocalizationTextResolver.Resolve(snapshot, "japanese", "content.empty.target"));
            Assert.AreEqual("English", candidate.Languages["english"]["content.empty.target"]);
        }

    }
}
