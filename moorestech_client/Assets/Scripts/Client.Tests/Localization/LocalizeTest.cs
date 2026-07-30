using System.Collections.Generic;
using System.Collections.ObjectModel;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Localization
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
            Localize.Initialize();

            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void TypedKeyReturnsTextForSelectedEnglishAndJapaneseLanguages()
        {
            Localize.Initialize();
            Localize.SetLanguage("english");
            var english = Localize.Get(LocalizationKeys.Ui.MainMenu.PlayLocally);

            Localize.SetLanguage("japanese");
            var japanese = Localize.Get(LocalizationKeys.Ui.MainMenu.PlayLocally);

            Assert.AreEqual("Play locally", english);
            Assert.AreEqual("ローカルでプレイ", japanese);
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
        public void GetLanguageCodesExcludesSourcePseudoLocale()
        {
            Localize.Initialize();

            CollectionAssert.AreEqual(new[] { "english", "japanese" }, Localize.GetLanguageCodes());
            CollectionAssert.DoesNotContain(Localize.GetLanguageCodes(), Localize.SourcePseudoLocale);
        }

        [Test]
        public void TryGetDictionaryReturnsSourceTextsAndRejectsUnknownLanguage()
        {
            Localize.Initialize();

            var foundSource = Localize.TryGetDictionary(Localize.SourcePseudoLocale, out var source);
            var foundUnknown = Localize.TryGetDictionary("unknown", out var unknown);

            Assert.IsTrue(foundSource);
            Assert.AreEqual("Play locally", source["ui.mainMenu.playLocally"]);
            Assert.IsFalse(foundUnknown);
            Assert.IsNull(unknown);
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
        public void SetLanguagePublishesExactlyOneEventAndPersistsSelection()
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, Localize.DefaultLanguageCode);
            Localize.Initialize();
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            Localize.SetLanguage("japanese");

            Assert.AreEqual(1, eventCount);
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual("japanese", PlayerPrefs.GetString(Localize.LanguagePreferenceKey));
        }

        [TestCase(Localize.SourcePseudoLocale)]
        [TestCase("unknown")]
        public void SetLanguageRejectsInvalidCodeWithoutChangingState(string invalidLanguageCode)
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, Localize.DefaultLanguageCode);
            Localize.Initialize();
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);
            LogAssert.Expect(LogType.Error, $"[Localize] Language Code : {invalidLanguageCode} is not found");

            Localize.SetLanguage(invalidLanguageCode);

            Assert.AreEqual(0, eventCount);
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
            Assert.AreEqual(Localize.DefaultLanguageCode, PlayerPrefs.GetString(Localize.LanguagePreferenceKey));
        }
    }
}
