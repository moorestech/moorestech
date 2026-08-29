using Client.Localization;
using Client.Starter.EventMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.EventMode
{
    // 起動言語の適用（AutoStartの適用行）が要求どおり実言語へ反映されることを守る
    // Guards that the launch-language apply step actually switches the real language
    public class EventModeLaunchLanguageTest
    {
        private bool hadSavedLanguageCode;
        private string savedLanguageCode;

        [SetUp]
        public void SetUp()
        {
            hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            // PlayerPrefsの言語をテスト前の状態へ正確に戻す
            // Restore the persisted language exactly as it was before the test
            if (hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        private static EventExhibitionSettings SettingsWithLanguage(string language)
        {
            return EventExhibitionSettings.Parse(new EventModeEnvironmentValues("1", null, null, language), false);
        }

        [Test]
        public void ApplyLaunchLanguage_KnownCode_SwitchesCurrentLanguage()
        {
            var result = EventModeAutoStart.ApplyLaunchLanguage(SettingsWithLanguage("german"));

            Assert.AreEqual(LanguageResolution.Accepted, result.Resolution);
            Assert.AreEqual("german", Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void ApplyLaunchLanguage_Unset_AppliesDefaultWithoutError()
        {
            Localize.TrySetLanguage("japanese");

            var result = EventModeAutoStart.ApplyLaunchLanguage(SettingsWithLanguage(null));

            Assert.AreEqual(LanguageResolution.Unset, result.Resolution);
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void ApplyLaunchLanguage_UnknownCode_FallsBackToDefaultAndLogsError()
        {
            Localize.TrySetLanguage("japanese");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("unknown MOORESTECH_EVENT_LANGUAGE=germn"));

            var result = EventModeAutoStart.ApplyLaunchLanguage(SettingsWithLanguage("germn"));

            Assert.AreEqual(LanguageResolution.UnknownFallback, result.Resolution);
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }
    }
}
