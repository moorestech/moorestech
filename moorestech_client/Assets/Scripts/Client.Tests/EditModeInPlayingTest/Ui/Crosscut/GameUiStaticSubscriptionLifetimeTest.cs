using System.Collections;
using Client.Input;
using Client.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.Crosscut
{
    /// <summary>
    /// GameUi の PlayMode を抜けた後、静的な源への購読が破棄済みの UI を叩かないことを確かめる
    /// Verifies subscriptions to static sources stop touching the destroyed UI after leaving a GameUi play mode
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class GameUiStaticSubscriptionLifetimeTest : UiSystemPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator PlayModeを抜けた後に言語を変えても破棄済みのUIを叩かない()
        {
            SelectUiSystemBeforePlayMode(UiSystem.GameUi);
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return LoadMainGame().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            LogAssert.ignoreFailingMessages = false;

            // 保存値を退避してから、ドメインを読み直さない Edit モードで言語を往復する
            // Preserve the persisted value, then round-trip the language in edit mode without a domain reload
            var hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            var savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.TrySetChosenLanguage("japanese");
            Localize.TrySetChosenLanguage("english");

            // 保存値と有無を復元し、言語状態を初期化する
            // Restore value and presence, then reset the language state
            if (hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();

            LogAssert.NoUnexpectedReceived();
        }
    }
}
