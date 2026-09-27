using System.Collections;
using System.Threading;
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

                // client のawait結果・ログ・例外を実際のPlayerLoop上で検証する
                // Verify client awaits, logs, and exceptions on a running PlayerLoop
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
                var main = Thread.CurrentThread.ManagedThreadId;
                var server = await RemoteExecRunner.RunAsync("return System.Threading.Thread.CurrentThread.ManagedThreadId;", RemoteExecTarget.Server);
                Assert.IsTrue(server.Ok, server.Exception);
                Assert.AreNotEqual(main.ToString(), server.Result);

                // 終了後の指定はキューに残さず理由付きで失敗する
                // Reject server work with a reason after its lifetime ends
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
