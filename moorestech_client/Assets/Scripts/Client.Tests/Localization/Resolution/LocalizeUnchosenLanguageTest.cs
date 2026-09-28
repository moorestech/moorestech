using Client.Localization;
using NUnit.Framework;
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

            Assert.IsTrue(Localize.TryApplyUnchosenLanguage("japanese"));
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual(1, changedCount);
            Assert.IsFalse(PlayerPrefs.HasKey(Localize.LanguagePreferenceKey));
            Assert.IsFalse(Localize.HasChosenLanguage());
        }

        [Test]
        public void KeepsChosenLanguage()
        {
            Localize.TrySetLanguage("japanese");

            Assert.IsFalse(Localize.TryApplyUnchosenLanguage("german"));
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.IsTrue(Localize.HasChosenLanguage());
        }

        [Test]
        public void UnselectableSavedValueIsNotAChoice()
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, "obsolete");
            Localize.Initialize();

            Assert.IsFalse(Localize.HasChosenLanguage());
            Assert.IsTrue(Localize.TryApplyUnchosenLanguage("german"));
            Assert.AreEqual("german", Localize.GetCurrentLanguageCode());
        }

        [TestCase("french")]
        [TestCase("")]
        public void RejectsUnselectableLanguage(string languageCode)
        {
            Assert.IsFalse(Localize.TryApplyUnchosenLanguage(languageCode));
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }
    }
}
