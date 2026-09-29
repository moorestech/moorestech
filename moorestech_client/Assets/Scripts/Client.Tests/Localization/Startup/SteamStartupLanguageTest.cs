using Client.Localization;
using Client.Starter.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Localization
{
    public class SteamStartupLanguageTest
    {
        private bool hadSavedLanguageCode;
        private string savedLanguageCode;

        [SetUp]
        public void SetUp()
        {
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

        // 拒否理由はLocalizeのログへ
        // The rejection reason surfaces on Localize's log
        [Test]
        public void EditorBootDoesNotReadSteamOrApplyLanguage()
        {
            Assert.IsTrue(Application.isEditor);
            LogAssert.Expect(LogType.Warning,
                "[Localize] temporary language unavailable: editor session does not consult Steam");

            Assert.IsFalse(SteamStartupLanguage.TryApplyOnce());

            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
            Assert.IsFalse(PlayerPrefs.HasKey(Localize.LanguagePreferenceKey));
        }
    }
}
