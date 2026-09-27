using System.Collections;
using Client.Game.Common;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot.Loop;
using UnityEditor;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.RemoteExec
{
    [Category("CiShardClientPlay3")]
    public class RemoteExecServerTargetTest
    {
        [UnityTest]
        public IEnumerator 実行先と非同期結果を返す()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // PlayerLoop上でawait結果を検証
                // Verify await results on the PlayerLoop
                var client = await RemoteExecRunner.RunAsync("UnityEngine.Debug.Log(\"hello\"); await UniTask.Yield(); return 2;", RemoteExecTarget.Client);
                Assert.IsTrue(client.Ok, client.Exception);
                Assert.AreEqual("2", client.Result);
                Assert.That(client.Logs, Has.Some.Contains("hello"));

                // 先行要求のawait中も後続のclient要求を入れない
                // Keep later client requests out while the first awaits
                var first = RemoteExecRunner.RunAsync("await UniTask.DelayFrame(2); UnityEngine.Debug.Log(\"first-end\"); return 1;", RemoteExecTarget.Client);
                var second = RemoteExecRunner.RunAsync("UnityEngine.Debug.Log(\"second-start\"); return 2;", RemoteExecTarget.Client);
                var firstResult = await first;
                var secondResult = await second;
                Assert.IsTrue(firstResult.Ok, firstResult.Exception);
                Assert.IsTrue(secondResult.Ok, secondResult.Exception);
                Assert.That(firstResult.Logs, Has.Some.Contains("first-end"));
                Assert.That(firstResult.Logs, Has.None.Contains("second-start"));

                var failed = await RemoteExecRunner.RunAsync("throw new System.InvalidOperationException(\"boom\");", RemoteExecTarget.Client);
                Assert.IsFalse(failed.Ok);
                Assert.That(failed.Exception, Does.Contain("boom"));

                // サーバー指定の同期部分は別更新スレッドで実行する
                // Run the synchronous server entry on its distinct update thread
                var server = await RemoteExecRunner.RunAsync("return System.Threading.Thread.CurrentThread.Name;", RemoteExecTarget.Server);
                Assert.IsTrue(server.Ok, server.Exception);
                Assert.AreEqual("[moorestech]ゲームアップデートスレッド", server.Result);

                // 送信コードの例外は応答に閉じ込め、後続tickを動かし続ける
                // Keep submitted exceptions in the response and allow later ticks to run
                var serverFailure = await RemoteExecRunner.RunAsync("throw new System.InvalidOperationException(\"server-boom\");", RemoteExecTarget.Server);
                Assert.IsFalse(serverFailure.Ok);
                Assert.That(serverFailure.Exception, Does.Contain("server-boom"));
                var afterFailure = await RemoteExecRunner.RunAsync("return 3;", RemoteExecTarget.Server);
                Assert.IsTrue(afterFailure.Ok, afterFailure.Exception);
                Assert.AreEqual("3", afterFailure.Result);

                // 終了後は理由付きで失敗
                // Fail with a reason after shutdown
                await GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
                Assert.IsFalse(ServerThreadActionQueue.HasDrainedThisLifetime, "終了完了直後はOnDestroyを待たず受付を閉じる");
                var stopped = await RemoteExecRunner.RunAsync("return 1;", RemoteExecTarget.Server);
                Assert.IsFalse(stopped.Ok);
                Assert.That(stopped.Exception, Does.Contain("サーバー"));
            }

            #endregion
        }
    }
}
