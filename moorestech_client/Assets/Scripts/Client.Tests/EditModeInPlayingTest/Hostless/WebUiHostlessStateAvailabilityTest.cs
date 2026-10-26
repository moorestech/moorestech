using System.Collections;
using Client.Tests.EditModeInPlayingTest.Util.World;
using UnityEngine;
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
    /// Web無しでもDIから状態が取れる
    /// States resolve from DI without the web host
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
                var ground = TestPlayerGround.Create();

                // 起動ログ排出し全probe検査
                // Drain startup logs and validate all probes
                await UniTask.Yield();
                LogAssert.ignoreFailingMessages = false;
                try
                {
                    await TestPlayerGround.AssertGroundedAsync();
                    // Hub=null経路を前提に固定
                    // Pin the hub-null path as a precondition
                    Assert.IsNull(Client.WebUiHost.Boot.WebUiHost.Hub, "Editor限定スイッチを付けたのにWebホストが起動した");
                    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                    Assert.IsNotNull(resolver);
                    await ModalStateProbe.AssertAvailableAsync(resolver);
                    await ResearchStateProbe.AssertAvailableAsync(resolver);
                    await ChallengeStateProbe.AssertAvailableAsync(resolver);
                    NotificationStateProbe.AssertAvailable(resolver);
                    await TimetableStateProbe.AssertAvailableAsync(resolver);
                    await BlockDetailStateProbe.AssertAvailableAsync(resolver);

                    // 破棄前に保存・終了を待つ
                    // Await saving and shutdown before destruction
                    await AssertIntentionalShutdownFlushedAsync("HostlessShutdown");
                }
                finally
                {
                    // 自分の足場だけ除去
                    // Remove only our ground
                    Object.DestroyImmediate(ground);
                    LogAssert.ignoreFailingMessages = true;
                }
            }

            #endregion
        }
    }
}
