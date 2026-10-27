using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.Paths;
using NUnit.Framework;
using Server.Boot;
using UnityEngine.TestTools;

namespace Client.Tests.Starter
{
    // Play停止や強制終了ではUnityの破棄とShutdownAsyncの順序が定まらない。どちらが先でも破棄済みの自分に触らず、終了時の世界を書き出す
    // On a play stop or forced quit, Unity's teardown and ShutdownAsync arrive in no fixed order; either way it must not touch its destroyed self and must write the final world
    public class ServerStarterPreemptedTeardownTest
    {
        [UnityTest]
        public IEnumerator 破棄がShutdownAsyncの待ちを追い越しても書き出し結果を返す()
        {
            var worldRoot = ServerStarterTestBoot.NewWorldRoot();
            var starter = ServerStarterTestBoot.Boot(worldRoot);

            // 書き出し待ちに入った直後にPlay停止と同じ順で畳む
            // Right after the flush wait begins, fold in the play-stop order
            var shutdown = starter.ShutdownAsync();
            ServerStarterTestBoot.Teardown(starter);

            yield return AssertFinalWorldWritten(shutdown, worldRoot);
        }

        [UnityTest]
        public IEnumerator 破棄がShutdownAsyncより先に来ても終了時の世界を書き出す()
        {
            var worldRoot = ServerStarterTestBoot.NewWorldRoot();
            var starter = ServerStarterTestBoot.Boot(worldRoot);

            // サーバー側のOnApplicationQuitがクライアントの終了表明より先に走った順序を再現する
            // Reproduce the order where the server's OnApplicationQuit runs before the client's shutdown declaration
            ServerStarterTestBoot.Teardown(starter);
            var shutdown = starter.ShutdownAsync();

            yield return AssertFinalWorldWritten(shutdown, worldRoot);
        }

        private static IEnumerator AssertFinalWorldWritten(UniTask<ServerSaveFlushResult> shutdown, string worldRoot)
        {
            for (var frame = 0; frame < 600 && shutdown.Status == UniTaskStatus.Pending; frame++) yield return null;
            var status = shutdown.Status;
            var result = status == UniTaskStatus.Succeeded ? shutdown.GetAwaiter().GetResult() : ServerSaveFlushResult.FlushTimedOut;
            var saveJsonPath = WorldDataDirectory.FromWorldRoot(worldRoot).SaveJsonFilePath;
            var saved = File.Exists(saveJsonPath);
            if (Directory.Exists(worldRoot)) Directory.Delete(worldRoot, true);

            Assert.AreEqual(UniTaskStatus.Succeeded, status, "破棄と順序が入れ替わった終了処理が例外で終わった（破棄済みの ServerStarter に触っている）");
            Assert.AreEqual(ServerSaveFlushResult.Flushed, result);
            Assert.IsTrue(saved, $"終了セーブのファイルが無い: {saveJsonPath}");
        }
    }
}
