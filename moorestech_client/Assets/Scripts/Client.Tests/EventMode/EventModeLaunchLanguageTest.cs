using System.Text.RegularExpressions;
using Client.Localization;
using Client.Starter.EventMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.EventMode
{
    // 起動言語が実言語へ反映されるか
    // Whether the launch language reaches the real language
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
            // 言語をテスト前の値へ戻す
            // Restore the language to its pre-test value
            if (hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        private static EventExhibitionSettings SettingsWithLanguage(string language)
        {
            var raw = new EventModeEnvironmentValues
            {
                Enable = "1",
                IdleTimeoutSeconds = null,
                EditorOptIn = null,
                Language = language,
            };
            return EventExhibitionSettings.Parse(raw, false);
        }

        [Test]
        public void ApplyLaunchLanguage_KnownCode_SwitchesCurrentLanguage()
        {
            EventModeAutoStart.ApplyLaunchLanguage(SettingsWithLanguage("german"));

            Assert.AreEqual("german", Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void ApplyLaunchLanguage_Unset_AppliesDefaultWithoutError()
        {
            Localize.TrySetLanguage("japanese");

            EventModeAutoStart.ApplyLaunchLanguage(SettingsWithLanguage(null));

            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void ApplyLaunchLanguage_UnknownCode_FallsBackToDefaultAndLogsError()
        {
            Localize.TrySetLanguage("japanese");
            LogAssert.Expect(LogType.Error, new Regex($"unknown {EventExhibitionSettings.LanguageEnvKey}=germn"));

            EventModeAutoStart.ApplyLaunchLanguage(SettingsWithLanguage("germn"));

            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }
    }
}
