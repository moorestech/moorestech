using System.Collections;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
#if UNITY_EDITOR
using Client.Starter.Initialization.Boot;
using UnityEditor;
#endif
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    /// <summary>
    /// Webホストを起動しないPlayModeでも、Webから移した論理状態がDIから取れることを確かめる（UITK段階0の完了条件）
    /// Verifies the logical states moved out of the web host are resolvable from DI in a Play Mode without the web host (UITK stage 0 exit criterion)
    /// </summary>
    [Category("CiShardClientPlay2")]
    public class WebUiHostlessStateAvailabilityTest
    {
        [UnityTest]
        public IEnumerator Webホストを起動しなくても移した状態がDIから取れる()
        {
            EnterPlayModeUtil();
#if UNITY_EDITOR
            WebUiHostEditorSkip.Apply();
#endif

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

#if UNITY_EDITOR
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
#endif

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // スイッチが効き、起動失敗と同じHub=nullの経路を通ったことを前提として固定する
                // Pin the precondition that the switch worked and the boot went down the hub-null path
                Assert.IsNull(Client.WebUiHost.Boot.WebUiHost.Hub, "Editor限定スイッチを付けたのにWebホストが起動した");
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                Assert.IsNotNull(resolver);
                await ModalStateProbe.AssertAvailableAsync(resolver);
                await ResearchStateProbe.AssertAvailableAsync(resolver);
                ChallengeStateProbe.AssertAvailable(resolver);
            }

            #endregion
        }
    }
}
