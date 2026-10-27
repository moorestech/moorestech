using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Client.Playtest.Core;
using Client.Tests.Starter;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest
{
    // DSLのサーバー書き込みはtickスレッドで走り、成否どちらでも続きはメインスレッドへ戻る。止まったサーバー・時間切れの処理は実行しない
    // DSL server writes run on the tick thread and continue on the main thread on success or failure; a stopped server or timed-out work never runs
    public class PlaytestServerThreadActionTest
    {
        private string _worldRoot;
        private ServerStarter _starter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // tick末尾が1度動くまでキューは受け付けないので、そこまで待つ
            // The queue admits nothing until a tick end has run once, so wait for it
            _worldRoot = ServerStarterTestBoot.NewWorldRoot();
            _starter = ServerStarterTestBoot.Boot(_worldRoot);
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!(ServerThreadActionQueueAccess.Current?.HasDrainedThisLifetime ?? false) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ServerThreadActionQueueAccess.Current?.HasDrainedThisLifetime ?? false, "サーバーのtick末尾が30秒以内に動かなかった");
        }

        [TearDown]
        public void TearDown()
        {
            ServerStarterTestBoot.Teardown(_starter);
            if (Directory.Exists(_worldRoot)) Directory.Delete(_worldRoot, true);
        }

        [UnityTest]
        public IEnumerator 処理はtickスレッドで走り完了後の続きはメインスレッドで走る()
        {
            var actionThreadId = -1;
            var continuedOnMainThread = false;
            var task = PlaytestServerThreadAction.RunAsync("thread probe", () => actionThreadId = Thread.CurrentThread.ManagedThreadId);
            task.GetAwaiter().OnCompleted(() => continuedOnMainThread = PlayerLoopHelper.IsMainThread);
            yield return WaitUntilDone(task, 15f);

            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.AreEqual(GameUpdateThread().ManagedThreadId, actionThreadId, "処理がサーバーのtickスレッドで実行されていない");
            Assert.IsTrue(continuedOnMainThread, "成功時の続きがメインスレッドで走っていない");
        }

        [UnityTest]
        public IEnumerator 処理が投げても続きはメインスレッドで走る()
        {
            LogAssert.Expect(LogType.Error, new Regex("tick末尾の処理が例外で失敗しました: throw probe"));
            var continuedOnMainThread = false;
            var task = PlaytestServerThreadAction.RunAsync("throw probe", () => throw new InvalidOperationException("scenario failure"));
            task.GetAwaiter().OnCompleted(() => continuedOnMainThread = PlayerLoopHelper.IsMainThread);
            yield return WaitUntilDone(task, 15f);

            // 例外は呼び出し元へ届き、その続き（シナリオの後始末）はメインスレッドで走る
            // The exception reaches the caller and its continuation (scenario cleanup) runs on the main thread
            Assert.AreEqual(UniTaskStatus.Faulted, task.Status);
            Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
            Assert.IsTrue(continuedOnMainThread, "失敗時の続きがtickスレッドで走った（メインスレッド専用APIの後始末が壊れる）");
        }

        [Test]
        public void 受付停止後は即座にInvalidOperationExceptionを返す()
        {
            LogAssert.Expect(LogType.Error, new Regex("サーバーがtick末尾の処理を受理できません: after stop"));
            typeof(ServerInstanceManager).GetMethod("StopThreadActions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ServerStarterTestBoot.InstanceManager(_starter), null);

            var ran = false;
            var task = PlaytestServerThreadAction.RunAsync("after stop", () => ran = true);

            Assert.AreEqual(UniTaskStatus.Faulted, task.Status, "停止後の呼び出しが待ちに入った");
            Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
            Assert.IsFalse(ran);
        }

        [UnityTest]
        public IEnumerator 時間切れの処理は後でtickが進んでも実行されない()
        {
            LogAssert.Expect(LogType.Error, new Regex("秒以内に終わりません（tickの中で止まっています）: blocker"));
            LogAssert.Expect(LogType.Error, new Regex("秒以内に始まらないため取り消しました: late"));

            // 先頭の処理でtickを止め、後ろの処理を時間切れにさせる
            // Freeze the tick with the first action so the one behind it times out
            using var release = new ManualResetEventSlim(false);
            var blocker = PlaytestServerThreadAction.RunAsync("blocker", () => release.Wait(TimeSpan.FromSeconds(30)));
            yield return null;
            var lateRan = false;
            var late = PlaytestServerThreadAction.RunAsync("late", () => lateRan = true);
            yield return WaitUntilDone(late, 20f);
            Assert.AreEqual(UniTaskStatus.Faulted, late.Status, "tickが止まっているのに時間切れにならなかった");
            Assert.Throws<TimeoutException>(() => late.GetAwaiter().GetResult());

            // tickを再開させ、後続のtick末尾が何度か回っても取り消した処理が走らないことを確かめる
            // Resume the tick and confirm the cancelled work stays unrun across several later tick ends
            release.Set();
            yield return WaitUntilDone(blocker, 20f);
            Assert.Throws<TimeoutException>(() => blocker.GetAwaiter().GetResult());
            var probe = PlaytestServerThreadAction.RunAsync("after release", () => { });
            yield return WaitUntilDone(probe, 15f);
            Assert.AreEqual(UniTaskStatus.Succeeded, probe.Status);
            Assert.IsFalse(lateRan, "時間切れで失敗扱いにした処理が後から実行された");
        }

        private Thread GameUpdateThread()
        {
            var manager = ServerStarterTestBoot.InstanceManager(_starter);
            return (Thread)typeof(ServerInstanceManager).GetField("_gameUpdateThread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        }

        private static IEnumerator WaitUntilDone(UniTask task, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (task.Status == UniTaskStatus.Pending && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
