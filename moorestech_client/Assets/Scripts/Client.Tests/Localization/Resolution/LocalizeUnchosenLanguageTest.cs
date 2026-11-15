using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.TestTools;
using UniRx;
using UnityEngine;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizeUnchosenLanguageTest
    {
        private bool hadSavedLanguageCode;
        private string savedLanguageCode;

        [SetUp]
        public void SetUp()
        {
            // 保存値を退避し、未選択から始める
            // Preserve the saved value and start unchosen
            hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [Test]
        public void AppliesWithoutPersistingWhenNothingIsChosen()
        {
            var changedCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => changedCount++);

            Assert.IsTrue(Localize.TryApplyUnchosenLanguage(new FixedSource(true, "japanese")));
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual(1, changedCount);
            Assert.IsFalse(PlayerPrefs.HasKey(Localize.LanguagePreferenceKey));
        }

        [Test]
        public void KeepsChosenLanguage()
        {
            Localize.TrySetChosenLanguage("japanese");

            var source = new FixedSource(true, "german");
            LogAssert.Expect(LogType.Log, "[Localize] temporary language rejected: the player already chose a language");
            Assert.IsFalse(Localize.TryApplyUnchosenLanguage(source));
            Assert.IsFalse(source.WasRead);
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void UnselectableSavedValueIsNotAChoice()
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, "obsolete");
            LogAssert.Expect(LogType.Warning, "[Localize] saved language obsolete is unavailable; using english");
            Localize.Initialize();

            Assert.IsTrue(Localize.TryApplyUnchosenLanguage(new FixedSource(true, "german")));
            Assert.AreEqual("german", Localize.GetCurrentLanguageCode());
        }

        [TestCase("french")]
        [TestCase("")]
        public void RejectsUnselectableLanguage(string languageCode)
        {
            LogAssert.Expect(LogType.Warning, $"[Localize] language rejected: unsupported code {languageCode}");
            Assert.IsFalse(Localize.TryApplyUnchosenLanguage(new FixedSource(true, languageCode)));
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void SourceFailureKeepsEnglishWithoutSaving()
        {
            LogAssert.Expect(LogType.Warning, "[Localize] temporary language unavailable: source unavailable");
            Assert.IsFalse(Localize.TryApplyUnchosenLanguage(new FixedSource(false, "japanese")));
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
            Assert.IsFalse(PlayerPrefs.HasKey(Localize.LanguagePreferenceKey));
        }

        [Test]
        public void TrySetChosenLanguagePublishesExactlyOneEventAndPersistsSelection()
        {
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            var applied = Localize.TrySetChosenLanguage("japanese");

            Assert.IsTrue(applied);
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual("japanese", PlayerPrefs.GetString(Localize.LanguagePreferenceKey));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(Localize.SourcePseudoLocale)]
        [TestCase("klingon")]
        public void TrySetChosenLanguageRejectsInvalidCodeWithoutChangingState(string invalidLanguageCode)
        {
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            LogAssert.Expect(LogType.Warning,
                $"[Localize] language rejected: unsupported code {invalidLanguageCode ?? "<null>"}");
            var applied = Localize.TrySetChosenLanguage(invalidLanguageCode);

            Assert.IsFalse(applied);
            Assert.AreEqual(0, eventCount);
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
            Assert.IsFalse(PlayerPrefs.HasKey(Localize.LanguagePreferenceKey));
        }

        /// <web-test file="src/shared/i18n/allScreensI18n.test.ts">marks a failed requested locale without replacing the previous ready dictionary</web-test>
        [Test]
        public void RejectedCodeAfterChoosingJapaneseKeepsJapaneseDictionary()
        {
            Assert.IsTrue(Localize.TrySetChosenLanguage("japanese"));
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            // 選べない言語の要求は拒否のログだけを残し、直前の日本語の辞書を差し替えない
            // A request for an unselectable language only logs the rejection and keeps the previous Japanese dictionary
            LogAssert.Expect(LogType.Warning, "[Localize] language rejected: unsupported code klingon");
            Assert.IsFalse(Localize.TrySetChosenLanguage("klingon"));

            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual("ローカルでプレイ", Localize.Get(LocalizationKeys.Ui.MainMenu.PlayLocally));
            Assert.AreEqual(0, eventCount);
            Assert.AreEqual("japanese", PlayerPrefs.GetString(Localize.LanguagePreferenceKey));
        }

        private sealed class FixedSource : IUnchosenLanguageSource
        {
            private readonly bool resolves;
            private readonly string languageCode;
            public bool WasRead { get; private set; }

            public FixedSource(bool resolves, string languageCode)
            {
                this.resolves = resolves;
                this.languageCode = languageCode;
            }

            public bool TryResolveGameLanguage(out string resolvedCode, out string failureReason)
            {
                WasRead = true;
                resolvedCode = languageCode;
                failureReason = resolves ? "" : "source unavailable";
                return resolves;
            }
        }
    }
}
