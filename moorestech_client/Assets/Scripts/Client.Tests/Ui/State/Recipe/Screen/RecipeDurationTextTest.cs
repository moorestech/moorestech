using Client.Localization;
using Client.Ui.State.Recipe.Logic;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Recipe.Screen
{
    /// <summary>
    /// 矢印の上の秒数の書式（L0。言語は english に固定して戻す）
    /// Format of the seconds above the arrow (L0; pins english and restores it)
    /// </summary>
    public class RecipeDurationTextTest
    {
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void PinEnglish()
        {
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
        }

        [TearDown]
        public void RestoreLanguage()
        {
            // 言語の保存値と有無を戻す（ScreenTestHost と同じ手順）
            // Restore the saved language value and its presence (same steps as ScreenTestHost)
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [TestCase(2d, "2 seconds")]
        [TestCase(0.4d, "0.4 seconds")]
        [TestCase(1.25d, "1.25 seconds")]
        public void 秒数をJSの数の文字化と同じ書式でui_recipe_durationへ入れる(double seconds, string expected)
        {
            Assert.AreEqual(expected, RecipeDurationText.Create(seconds).Resolve());
        }
    }
}
