using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Client.Playtest.Core;
using Cysharp.Threading.Tasks;
using Game.Paths;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Starter
{
    // tickが止まらないまま畳まれたら、終了時セーブを並行に取り込まず、保存されない旨を開発者が読めるログへ残す
    // When folding while the tick will not stop, never capture the shutdown save concurrently, and log for developers that the world stays unsaved
    public class ServerStarterStalledTickTeardownTest
    {
        [UnityTest]
        public IEnumerator tickが止まらないまま畳むと終了時セーブを取り込まずLogErrorを出す()
        {
            var worldRoot = ServerStarterTestBoot.NewWorldRoot();
            var starter = ServerStarterTestBoot.Boot(worldRoot);
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!(ServerThreadActionQueueAccess.Current?.HasDrainedThisLifetime ?? false) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ServerThreadActionQueueAccess.Current?.HasDrainedThisLifetime ?? false, "サーバーのtick末尾が30秒以内に動かなかった");
            var manager = ServerStarterTestBoot.InstanceManager(starter);
            var gameUpdateThread = (Thread)typeof(ServerInstanceManager).GetField("_gameUpdateThread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

            // tick末尾の処理でtickスレッドを止めたまま、Play停止と同じ破棄経路で畳む
            // Fold through the play-stop teardown while a tick-end action holds the tick thread
            using var release = new ManualResetEventSlim(false);
            using var blockerStarted = new ManualResetEventSlim(false);
            var blocker = PlaytestServerThreadAction.RunAsync("stall blocker", () => { blockerStarted.Set(); release.Wait(TimeSpan.FromSeconds(60)); });
            while (!blockerStarted.IsSet && Time.realtimeSinceStartup < deadline + 15f) yield return null;
            Assert.IsTrue(blockerStarted.IsSet, "tickを止める処理が始まらなかった");
            LogAssert.Expect(LogType.Error, new Regex("処理完了前にサーバーが停止しました: stall blocker"));
            LogAssert.Expect(LogType.Error, new Regex("ゲーム更新threadがcancel後5秒で停止しませんでした"));
            LogAssert.Expect(LogType.Error, new Regex("ゲーム更新threadが停止しないため、終了時に要求されたセーブを取り込めません"));
            LogAssert.Expect(LogType.Error, new Regex("終了時に要求されたセーブが書き出されないまま、サーバーを終了します"));
            ServerStarterTestBoot.Teardown(starter);
            var saveJsonPath = WorldDataDirectory.FromWorldRoot(worldRoot).SaveJsonFilePath;
            var savedWhileStalled = File.Exists(saveJsonPath);

            // tickを再開させ、スレッドの終了を待ってからワールドを消す。再開したtickは閉じた書き出しへ投入しようとして例外ログを出し、何も書かない
            // Resume the tick and wait for the thread to end before deleting the world; the resumed tick logs an exception enqueueing into the closed writer and writes nothing
            LogAssert.Expect(LogType.Exception, new Regex("marked as complete with regards to additions"));
            release.Set();
            var threadEnded = gameUpdateThread.Join(TimeSpan.FromSeconds(15));
            yield return WaitUntilDone(blocker, 15f);
            if (Directory.Exists(worldRoot)) Directory.Delete(worldRoot, true);

            Assert.IsFalse(savedWhileStalled, "tickが動いたまま終了時セーブを取り込んで書き出した");
            Assert.IsTrue(threadEnded, "再開後もゲーム更新threadが終わらない");
            Assert.Throws<InvalidOperationException>(() => blocker.GetAwaiter().GetResult());
        }

        private static IEnumerator WaitUntilDone(UniTask task, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (task.Status == UniTaskStatus.Pending && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
