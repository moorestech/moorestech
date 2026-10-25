using System.Collections;
using Client.Game.Common;
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

                // 起動ログを排出してから、全プローブの想定外エラーを検査する
                // Drain startup logs, then validate unexpected errors across all probes
                await UniTask.Yield();
                LogAssert.ignoreFailingMessages = false;
                try
                {
                    // スイッチが効き、起動失敗と同じHub=nullの経路を通ったことを前提として固定する
                    // Pin the precondition that the switch worked and the boot went down the hub-null path
                    Assert.IsNull(Client.WebUiHost.Boot.WebUiHost.Hub, "Editor限定スイッチを付けたのにWebホストが起動した");
                    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                    Assert.IsNotNull(resolver);
                    await ModalStateProbe.AssertAvailableAsync(resolver);
                    await ResearchStateProbe.AssertAvailableAsync(resolver);
                    ChallengeStateProbe.AssertAvailable(resolver);
                    NotificationStateProbe.AssertAvailable(resolver);
                    await TimetableStateProbe.AssertAvailableAsync(resolver);
                    await BlockDetailStateProbe.AssertAvailableAsync(resolver);

                    // Unityが破棄する前に保存と終了参加者の完了を待つ
                    // Await saving and shutdown participants before Unity destroys the scene
                    var flushResult = await GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
                    Debug.Log($"[HostlessShutdown] result:{flushResult}");
                    Assert.AreEqual(ShutdownFlushResult.Flushed, flushResult);
                    await UniTask.Yield();
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = true;
                }
            }

            #endregion
        }
    }
}
