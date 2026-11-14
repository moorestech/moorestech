using System.Collections;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Util.Boot
{
    /// <summary>
    /// LoadMainGame は初期化の完了を待ってから戻る。戻った直後に足場を置くテストが自機の配置と競合しないため
    /// LoadMainGame returns only after initialization completes, so tests placing ground right after it never race the player placement
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay3")]
    public class LoadMainGameWaitsInitializationTest
    {
        [UnityTest]
        public IEnumerator LoadMainGameは初期化の完了を待ってから戻る()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                // 起動より前に購読し、戻った時点で完了の通知が来ているかを見る
                // Subscribe before booting and check that completion has arrived by the time it returns
                var initializedAt = -1f;
                using var initializedSubscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => initializedAt = Time.realtimeSinceStartup);
                await LoadMainGame();
                var returnedAt = Time.realtimeSinceStartup;
                var initializedAtReturn = initializedAt >= 0f;

                // 戻りと完了の前後関係を秒で残す（負なら戻った後に完了した）
                // Log the return-vs-completion gap in seconds (negative means completion came after the return)
                await UniTask.WaitUntil(() => initializedAt >= 0f).Timeout(System.TimeSpan.FromSeconds(180));
                Debug.Log($"[LoadMainGameWaitsInitializationTest] returnedAt - initializedAt = {returnedAt - initializedAt:F2}s");
                Assert.IsTrue(initializedAtReturn, "LoadMainGame が GameInitializedEvent より先に戻った");
            }

            #endregion
        }
    }
}
