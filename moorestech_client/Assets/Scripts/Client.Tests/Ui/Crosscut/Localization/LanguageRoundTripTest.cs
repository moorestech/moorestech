using Client.Localization;
using Client.Tests.Ui.Screens;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.Settings;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Crosscut.Localization
{
    /// <summary>
    /// ポーズの設定ページの文が言語の切替で辞書の値へ描き直され、言語の選択肢で英語と日本語を往復できることを確かめる（L1。Web e2e i18n.spec の写し）
    /// Verifies the pause settings page redraws from the dictionary on a language switch and round-trips English and Japanese via the selector (L1; port of the Web e2e i18n.spec)
    /// </summary>
    /// <remarks>
    /// 言語の保存値と有無の退避・復元は基底の ScreenTestHost が持つ（英語から始まる）
    /// The base ScreenTestHost preserves and restores the saved language and its presence (each test starts in English)
    /// </remarks>
    public class LanguageRoundTripTest : ScreenTestHost
    {
        private VisualElement _page;

        [SetUp]
        public void MountSettingsPage()
        {
            // 言語選択は本物のコマンドで通す
            // Language choices go through the real commands all the way to Localize
            var settings = new PauseMenuSettingsPage(FakeSettingsModel.Default(), new RecordingPauseMenuCommands(), new SettingsCommands(), Toaster, new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            _page = Mount(settings.Root);
        }

        /// <web-test file="e2e/tests/system/i18n.spec.ts">localization.current切替でlocale別辞書を再取得し表示を更新する</web-test>
        [Test]
        public void 言語を切り替えると見出しが切替先の辞書の文になる()
        {
            Assert.AreEqual(DictionaryText("english"), LanguageHeading().SourceText);

            Assert.IsTrue(Localize.TrySetChosenLanguage("japanese"));
            simulate.FrameUpdate();

            Assert.AreEqual(DictionaryText("japanese"), LanguageHeading().SourceText);
            Assert.AreNotEqual(DictionaryText("english"), DictionaryText("japanese"), "英語と日本語の見出しが同じで切替を確かめられない");
        }

        /// <web-test file="e2e/tests/system/i18n.spec.ts">ポーズメニューの言語選択で英語と日本語を往復する</web-test>
        [Test]
        public void 言語の選択肢で英語と日本語を往復する()
        {
            simulate.Click(Find<Button>(_page, "language-select-option-japanese"));
            simulate.FrameUpdate();
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual(DictionaryText("japanese"), LanguageHeading().SourceText);

            simulate.Click(Find<Button>(_page, "language-select-option-english"));
            simulate.FrameUpdate();
            Assert.AreEqual("english", Localize.GetCurrentLanguageCode());
            Assert.AreEqual(DictionaryText("english"), LanguageHeading().SourceText);
            Assert.IsEmpty(ShownToasts, "言語の切替が拒否のトーストを出した");
        }

        // 言語の節の見出しは設定ページの最初の節の題。text は行送りのタグ付きなので解決した文で比べる
        // The language heading is the first section title; text carries the line-height tag, so compare the resolved words
        private LocalizedLabel LanguageHeading()
        {
            var heading = _page.Q<LocalizedLabel>(className: "settings-section__title");
            Assert.IsNotNull(heading, "言語の節の見出しが無い");
            return heading;
        }

        private static string DictionaryText(string languageCode)
        {
            Assert.IsTrue(Localize.TryGetDictionary(languageCode, out var dictionary), languageCode);
            return dictionary[LocalizationKeys.Ui.Settings.Language.Key];
        }
    }
}
