using System;
using System.Linq;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Text
{
    /// <summary>
    /// Prefix は辞書キー以外の文もそのまま並べることの確認
    /// Checks that Prefix lays out non-dictionary text as is
    /// </summary>
    public class ShortcutHintLabelPrefixTextTest : UiTestHost
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
        public void prefixは辞書キー以外の文でも例外を出さずkbdの後ろに文を置く()
        {
            var content = new ShortcutHintLabel("content", ShortcutHintLayout.Prefix);
            var verbatim = new ShortcutHintLabel("verbatim", ShortcutHintLayout.Prefix);
            rootVisualElement.Add(content);
            rootVisualElement.Add(verbatim);

            // 以前は WithLeadingParam が辞書キー以外で InvalidOperationException を投げた
            // WithLeadingParam used to throw InvalidOperationException for non-dictionary text
            Assert.DoesNotThrow(() => content.SetHint(UiText.FromContent(ContentLocalizationKeys.ChallengeTutorialText(new Guid("8f2c6a1e-3b4d-4c5e-9f60-7a8b9c0d1e2f"))), "Tab"));
            Assert.DoesNotThrow(() => verbatim.SetHint(UiText.FromVerbatim("Open the menu"), "Esc"));
            simulate.FrameUpdate();

            Assert.AreEqual("Open the menu", ((Label)verbatim.Children().Last()).text);
            Assert.IsInstanceOf<KeyCap>(verbatim.Children().First(child => child.resolvedStyle.display == DisplayStyle.Flex));
        }
    }
}
