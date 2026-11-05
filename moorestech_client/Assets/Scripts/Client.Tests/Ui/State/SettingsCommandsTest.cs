using Client.Localization;
using Client.Ui.State.Commands;
using Client.Ui.State.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State
{
    public class SettingsCommandsTest
    {
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void SetUp()
        {
            // 言語の静的状態を既知の英語へ揃え、辞書を読み込む
            // Put the static locale state into a known English state and load the dictionaries
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, Localize.DefaultLanguageCode);
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [TestCase("unknown")]
        [TestCase("")]
        [TestCase(null)]
        public void 不明な言語コードはUnknownLanguageで拒否し現在言語を変えない(string languageCode)
        {
            var before = Localize.GetCurrentLanguageCode();

            var result = new SettingsCommands().SetLanguage(languageCode);

            Assert.IsInstanceOf<UiCommandRejected>(result);
            var rejected = (UiCommandRejected)result;
            Assert.AreEqual(UiCommandRejectReason.UnknownLanguage, rejected.Reason);
            Assert.AreEqual("unknown_locale", rejected.ToWireCode());
            Assert.AreEqual(before, Localize.GetCurrentLanguageCode());
        }
    }
}
