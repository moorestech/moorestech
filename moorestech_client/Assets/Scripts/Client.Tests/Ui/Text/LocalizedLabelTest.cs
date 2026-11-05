using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// LocalizedLabel が Attach 中だけ言語変更で解決し直し、引数を差し込むことを確かめる（L1）
    /// Verifies LocalizedLabel re-resolves on language change only while attached and interpolates params (L1)
    /// </summary>
    public class LocalizedLabelTest : UiTestHost
    {
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void SetUpLanguage()
        {
            // 保存値を退避し、英語の辞書から始める
            // Preserve the persisted value and start from the English dictionary
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
        }

        [TearDown]
        public void TearDownLanguage()
        {
            // 保存値と有無を復元し、言語状態を初期化する
            // Restore value and presence, then reset the language state
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [Test]
        public void ResolvesCurrentLanguageOnAttach()
        {
            var label = new LocalizedLabel();
            label.SetKey(LocalizationKeys.Ui.MainMenu.PlayLocally);
            rootVisualElement.Add(label);

            Assert.AreEqual("Play locally", label.text);
        }

        [Test]
        public void ReResolvesWhenLanguageChanges()
        {
            var label = new LocalizedLabel();
            label.SetKey(LocalizationKeys.Ui.MainMenu.PlayLocally);
            rootVisualElement.Add(label);

            Localize.TrySetChosenLanguage("japanese");

            Assert.AreEqual("ローカルでプレイ", label.text);
        }

        [Test]
        public void StopsFollowingLanguageAfterDetach()
        {
            var label = new LocalizedLabel();
            label.SetKey(LocalizationKeys.Ui.MainMenu.PlayLocally);
            rootVisualElement.Add(label);
            label.RemoveFromHierarchy();

            Localize.TrySetChosenLanguage("japanese");

            Assert.AreEqual("Play locally", label.text);
        }

        [Test]
        public void InterpolatesParams()
        {
            var label = new LocalizedLabel();
            label.SetKey(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount, new[] { new LocalizationTextParam("count", "3") });
            rootVisualElement.Add(label);
            Assert.AreEqual("Selected: 3", label.text);

            Localize.TrySetChosenLanguage("japanese");
            Assert.AreEqual("選択中: 3件", label.text);
        }
    }
}
