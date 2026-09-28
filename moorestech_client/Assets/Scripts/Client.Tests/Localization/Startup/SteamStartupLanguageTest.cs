using System.Text.RegularExpressions;
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
            // 保存値を退避し、起動時の未選択状態を作る
            // Preserve the saved value and start without a choice
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
        public void AppliesMappedSteamLanguageWithoutPersisting()
        {
            SteamStartupLanguage.Apply(new FixedReader(true, "japanese"));
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.IsFalse(PlayerPrefs.HasKey(Localize.LanguagePreferenceKey));
        }

        [Test]
        public void ChosenLanguageWinsOverSteam()
        {
            Localize.TrySetLanguage("german");
            SteamStartupLanguage.Apply(new FixedReader(true, "japanese"));
            Assert.AreEqual("german", Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void UnreadableSteamStaysEnglish()
        {
            LogAssert.Expect(LogType.Log, new Regex("staying on"));
            SteamStartupLanguage.Apply(new FixedReader(false, ""));
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }

        [Test]
        public void UnmappedSteamLanguageIsEnglish()
        {
            LogAssert.Expect(LogType.Log, new Regex("staying on"));
            SteamStartupLanguage.Apply(new FixedReader(true, "french"));
            Assert.AreEqual(Localize.DefaultLanguageCode, Localize.GetCurrentLanguageCode());
        }

        private sealed class FixedReader : ISteamGameLanguageReader
        {
            private readonly bool readable;
            private readonly string language;

            public FixedReader(bool readable, string language)
            {
                this.readable = readable;
                this.language = language;
            }

            public bool TryRead(out string steamLanguage, out string failureReason)
            {
                steamLanguage = language;
                failureReason = readable ? "" : "steam not running";
                return readable;
            }
        }
    }
}
