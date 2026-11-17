using System.Linq;
using Client.Localization;
using Client.Tests.Ui.State.Toast;
using Client.Ui.State.Commands;
using Client.Ui.State.Settings;
using NUnit.Framework;
using UniRx;
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

        [Test]
        public void 選べる言語は受理して現在言語と保存値を変え通知を1回出す()
        {
            var changes = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => changes++);

            var toaster = new RecordingUiCommandResultToaster();
            new SettingsCommands(toaster).SetLanguage("japanese");

            Assert.AreEqual(SettingsCommands.SetLocaleCommandName, toaster.Reports.Single().CommandName);
            Assert.IsInstanceOf<UiCommandAccepted>(toaster.Reports.Single().Result);
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual("japanese", PlayerPrefs.GetString(Localize.LanguagePreferenceKey));
            Assert.AreEqual(1, changes);
        }

        [TestCase("unknown")]
        [TestCase("")]
        [TestCase(null)]
        public void 不明な言語コードはUnknownLanguageで拒否し現在言語を変えない(string languageCode)
        {
            var before = Localize.GetCurrentLanguageCode();

            var toaster = new RecordingUiCommandResultToaster();
            new SettingsCommands(toaster).SetLanguage(languageCode);

            Assert.IsInstanceOf<UiCommandRejected>(toaster.Reports.Single().Result);
            var rejected = (UiCommandRejected)toaster.Reports.Single().Result;
            Assert.AreEqual(UiCommandRejectReason.UnknownLanguage, rejected.Reason);
            Assert.AreEqual("unknown_locale", rejected.ToWireCode());
            Assert.AreEqual(before, Localize.GetCurrentLanguageCode());
        }
    }
}
