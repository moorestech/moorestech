// moorestech_client/Assets/Scripts/Client.Tests/Ui/Text/LocalizedTextBindingTest.cs
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// 解決・言語変更での解き直しの確認
    /// Checks resolve and re-resolve on language change
    /// </summary>
    public class LocalizedTextBindingTest : UiTestHost
    {
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void UseEnglish()
        {
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
        }

        [TearDown]
        public void RestoreLanguage()
        {
            rootVisualElement.Clear();
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [Test]
        public void 載る前は流さず載ると解いた文を流し言語変更で解き直す()
        {
            var host = new VisualElement { name = "host" };
            var resolved = Observe(host, out var binding);
            binding.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Back));
            Assert.IsEmpty(resolved);

            rootVisualElement.Add(host);
            Localize.TrySetChosenLanguage("japanese");
            CollectionAssert.AreEqual(new[] { "Back", "戻る" }, resolved);
        }

        [Test]
        public void パネルから外れると言語変更を追わない()
        {
            var host = new VisualElement { name = "host" };
            var resolved = Observe(host, out var binding);
            binding.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Back));
            rootVisualElement.Add(host);
            rootVisualElement.Remove(host);

            Localize.TrySetChosenLanguage("japanese");
            CollectionAssert.AreEqual(new[] { "Back" }, resolved);
        }

        [Test]
        public void 載った後に作っても文を流し言語変更で解き直す()
        {
            var host = new VisualElement { name = "host" };
            rootVisualElement.Add(host);
            var resolved = Observe(host, out var binding);
            binding.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Back));

            Localize.TrySetChosenLanguage("japanese");
            CollectionAssert.AreEqual(new[] { "Back", "戻る" }, resolved);
        }

        [Test]
        public void 文無しで載るとLogErrorを出し何も流さない()
        {
            var host = new VisualElement { name = "host" };
            var resolved = Observe(host, out _);
            LogAssert.Expect(LogType.Error, new Regex(@"\[LocalizedTextBinding\] attached without a text: host"));
            rootVisualElement.Add(host);
            simulate.FrameUpdate();
            Assert.IsEmpty(resolved);
        }

        [Test]
        public void 載せた直後にSetTextすればLogErrorを出さない()
        {
            var host = new VisualElement { name = "host" };
            var resolved = Observe(host, out var binding);
            rootVisualElement.Add(host);
            binding.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Back));
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "Back" }, resolved);
        }

        private static List<string> Observe(VisualElement host, out LocalizedTextBinding binding)
        {
            binding = new LocalizedTextBinding(host);
            var resolved = new List<string>();
            binding.OnResolved.Subscribe(resolved.Add);
            return resolved;
        }
    }
}
