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

        // 未指定はSteam言語を潰さない
        // Unset never overwrites Steam's language
        [Test]
        public void ApplyLaunchLanguage_Unset_LeavesUnchosenLanguageUntouched()
        {
            PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Assert.IsTrue(Localize.TryApplyUnchosenLanguage(new FixedUnchosenSource("japanese")));

            EventModeAutoStart.ApplyLaunchLanguage(SettingsWithLanguage(null));

            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.IsFalse(PlayerPrefs.HasKey(Localize.LanguagePreferenceKey));
        }

        [Test]
        public void ApplyLaunchLanguage_UnknownCode_KeepsCurrentLanguageAndLogsError()
        {
            Localize.TrySetChosenLanguage("japanese");
            LogAssert.Expect(LogType.Error, new Regex($"unknown {EventExhibitionSettings.LanguageEnvKey}=germn"));

            EventModeAutoStart.ApplyLaunchLanguage(SettingsWithLanguage("germn"));

            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
        }

        private sealed class FixedUnchosenSource : IUnchosenLanguageSource
        {
            private readonly string languageCode;

            public FixedUnchosenSource(string languageCode)
            {
                this.languageCode = languageCode;
            }

            public bool TryResolveGameLanguage(out string resolvedCode, out string failureReason)
            {
                resolvedCode = languageCode;
                failureReason = "";
                return true;
            }
        }
    }
}
