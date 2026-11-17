using Client.Input;
using Client.Ui.State.Settings;
using NUnit.Framework;
using UnityEngine;
using Client.Tests.Ui.State.Toast;

namespace Client.Tests.Ui.State
{
    public class SettingsModelTest
    {
        private bool _hadSavedMode;
        private string _savedMode;

        [SetUp]
        public void SetUp()
        {
            // 端末の保存値を退避し、未保存（既定のトグル）から始める
            // Stash the device's saved value and start from unsaved (the default toggle)
            _hadSavedMode = PlayerPrefs.HasKey(SprintInputModePreference.PreferenceKey);
            _savedMode = PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSavedMode) PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, _savedMode);
            else PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void ダッシュ方式のコマンドは保存しViewModelに届く()
        {
            var model = new SettingsModel();
            model.Initialize();
            Assert.AreEqual(SprintInputMode.Toggle, model.Current.Value.SprintMode);

            new SettingsCommands(new RecordingUiCommandResultToaster()).SetSprintInputMode(SprintInputMode.Hold);

            Assert.AreEqual(SprintInputMode.Hold, model.Current.Value.SprintMode);
            Assert.AreEqual(SprintInputMode.Hold.ToString(), PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey));
            Assert.IsFalse(model.Current.Value.SprintRecovered);
        }
    }
}
