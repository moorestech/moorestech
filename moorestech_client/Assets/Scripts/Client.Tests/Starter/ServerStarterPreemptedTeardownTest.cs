using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.Paths;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Tests.Starter
{
    // Play停止や強制終了ではUnityの破棄がShutdownAsyncの書き出し待ちを追い越す。追い越されても破棄済みの自分に触らず、書き出し結果を返す
    // On a play stop or forced quit, Unity's teardown overtakes ShutdownAsync's flush wait; even then it must not touch its destroyed self and must return the flush result
    public class ServerStarterPreemptedTeardownTest
    {
        [UnityTest]
        public IEnumerator 破棄がShutdownAsyncの待ちを追い越しても書き出し結果を返す()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech-preempted-{Guid.NewGuid():N}");
            var gameObject = new GameObject("ServerStarterPreemptedTeardownTest");
            var starter = gameObject.AddComponent<ServerStarter>();
            starter.SetArgs(new[]
            {
                "--worldDirectory", worldRoot,
                "--mapMode", "template",
                "--port", "0",
                "--autoSave", "false",
                "--serverDataDirectory", TestModDirectory.ForUnitTestModDirectory,
            });

            // EditModeではStartが呼ばれないので、生成時と同じ起動をここで行う
            // EditMode never calls Start, so perform the same boot it does on creation
            typeof(ServerStarter).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(starter, null);

            // 書き出し待ちに入った直後にPlay停止と同じ即時破棄を起こす
            // Right after the flush wait begins, destroy immediately as a play stop does
            var shutdown = starter.ShutdownAsync();
            Object.DestroyImmediate(gameObject);

            for (var frame = 0; frame < 600 && shutdown.Status == UniTaskStatus.Pending; frame++) yield return null;
            var status = shutdown.Status;
            var result = status == UniTaskStatus.Succeeded ? shutdown.GetAwaiter().GetResult() : ServerSaveFlushResult.FlushTimedOut;
            var saveJsonPath = WorldDataDirectory.FromWorldRoot(worldRoot).SaveJsonFilePath;
            var saved = File.Exists(saveJsonPath);
            if (Directory.Exists(worldRoot)) Directory.Delete(worldRoot, true);

            Assert.AreEqual(UniTaskStatus.Succeeded, status, "破棄に追い越された終了処理が例外で終わった（破棄済みの ServerStarter に触っている）");
            Assert.AreEqual(ServerSaveFlushResult.Flushed, result);
            Assert.IsTrue(saved, $"終了セーブのファイルが無い: {saveJsonPath}");
        }
    }
}
