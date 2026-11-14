using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Client.Input;
using Client.Localization;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text.WrapSafe;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.Crosscut
{
    /// <summary>
    /// GameUi の実ゲームで日本語へ切り替えた次のフレームに、ルート以下のどの Label にも英語の文が残らないことを確かめる（L3。Web allScreensI18n.test の写し）
    /// Verifies in the real GameUi game that no Label under the root keeps English copy the frame after switching to Japanese (L3; port of the Web allScreensI18n.test)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class AllScreensLanguageSwitchTest : UiSystemPlayModeTestBase
    {
        /// <web-test file="src/shared/i18n/allScreensI18n.test.ts">re-renders translated screen copy after localization.current changes with no legacy screen exemptions</web-test>
        [UnityTest]
        public IEnumerator 日本語へ切り替えるとどの画面のLabelにも英語の文が残らない()
        {
            SelectUiSystemBeforePlayMode(UiSystem.GameUi);
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();

                // 保存値は全 worktree 共通なので退避し、英語から始める
                // The saved value is shared by every worktree, so preserve it and start from English
                var hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
                var savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
                var startLanguageCode = Localize.GetCurrentLanguageCode();
                var switchedToEnglish = Localize.TrySetChosenLanguage("english");
                await UniTask.DelayFrame(2);

                // 英語の文のうち日本語と違うものの集合を辞書から作り、英語のうちに画面に出ていること（空振りでない対照）を見る
                // Build the set of English texts that differ from Japanese, and confirm some show on screen while in English (non-vacuous control)
                var englishOnly = EnglishTextsDifferingFromJapanese();
                var shownInEnglish = LabelsShowing(englishOnly).Length;

                // 日本語へ切り替えた次のフレームに英語の文を出す Label を集める。除外の画面は作らない
                // Collect Labels still showing English copy the frame after switching to Japanese; no screen is exempted
                var switchedToJapanese = Localize.TrySetChosenLanguage("japanese");
                await UniTask.NextFrame();
                var leftovers = LabelsShowing(englishOnly);

                // 主張の前に言語と保存値を戻す（赤で抜けても他のテストへ漏らさない）
                // Restore the language and the saved value before asserting, so a red run leaks nothing into other tests
                Localize.TrySetChosenLanguage(startLanguageCode);
                if (hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
                else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
                PlayerPrefs.Save();

                Assert.IsTrue(switchedToEnglish, "英語へ切り替わらない");
                Assert.IsTrue(switchedToJapanese, "日本語へ切り替わらない");
                Assert.Greater(shownInEnglish, 0, "英語のうちに辞書の英語の文を出す Label が無く、確かめにならない");
                Assert.IsEmpty(leftovers, $"日本語へ切り替えても英語の文が残る Label: {string.Join(", ", leftovers)}");
            }

            #endregion
        }

        private static HashSet<string> EnglishTextsDifferingFromJapanese()
        {
            Assert.IsTrue(Localize.TryGetDictionary("english", out var english), "英語の辞書が無い");
            Assert.IsTrue(Localize.TryGetDictionary("japanese", out var japanese), "日本語の辞書が無い");
            var texts = new HashSet<string>();
            foreach (var pair in english)
            {
                if (japanese.TryGetValue(pair.Key, out var japaneseText) && japaneseText != pair.Value) texts.Add(pair.Value);
            }
            return texts;
        }

        // 英語の文の集合に入る文を持つ Label を「名前 文」で挙げる。WrapSafeLabel の text は行送りのタグ付きなので文そのもので比べる
        // List Labels whose words are in the English set as "name words"; a WrapSafeLabel's text carries the line-height tag, so compare its words
        private static string[] LabelsShowing(HashSet<string> englishTexts)
        {
            return GameUiRoot.Instance.GetRootElement().Query<Label>().ToList()
                .Select(label => (label.name, words: label is WrapSafeLabel wrapSafe ? wrapSafe.WordsText : label.text))
                .Where(shown => englishTexts.Contains(shown.words))
                .Select(shown => $"'{shown.name}' {shown.words}")
                .ToArray();
        }
    }
}
