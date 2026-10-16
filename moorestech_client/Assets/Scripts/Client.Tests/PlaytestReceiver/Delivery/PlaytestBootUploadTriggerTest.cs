using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Client.PlaytestReceiver.Upload;
using UnityEngine.TestTools;
using Client.Game.InGame.BugReport.Submit;
using Client.Starter.Playtest;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestBootUploadTriggerTest
    {
        [UnityTest]
        public IEnumerator 起動時に一度だけ送信を要求する()
        {
            var requester = new CountingRequester();
            var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            new PlaytestBootUploadTrigger(requester, PlaytestOutboxTestBoxes.Directories(root)).Start();
            for (var frame = 0; requester.Count == 0 && frame < 300; frame++) yield return null;
            Directory.Delete(root, true);
            Assert.AreEqual(1, requester.Count);
        }
        [UnityTest]
        public IEnumerator ゲート待機中に終了すると送信要求を残さない()
        {
            var requester = new CountingRequester();
            var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var cancellation = new CancellationTokenSource();
            var trigger = new PlaytestBootUploadTrigger(requester, PlaytestOutboxTestBoxes.Directories(root));
            Assert.IsTrue(PlaytestOutboxAccess.Gate.Wait(0));
            try
            {
                // 実際の起動処理を待機させ、再生終了と同じキャンセルを注入する
                // Suspend the real boot operation and inject the cancellation used on play exit
                var method = typeof(PlaytestBootUploadTrigger).GetMethod("CompleteAndRequestAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                var operation = ((UniTask)method.Invoke(trigger, new object[] { cancellation.Token })).AsTask();
                Assert.IsFalse(operation.IsCompleted);
                cancellation.Cancel();
                for (var frame = 0; !operation.IsCompleted && frame < 300; frame++) yield return null;
                Assert.IsTrue(operation.IsCompleted);
                Assert.That(operation.Exception?.InnerException, Is.InstanceOf<System.OperationCanceledException>());
                Assert.AreEqual(0, requester.Count);
            }
            finally
            {
                PlaytestOutboxAccess.Gate.Release();
                Directory.Delete(root, true);
            }
        }

        private sealed class CountingRequester : IPlaytestUploadRequester
        {
            internal int Count;
            public void RequestUpload() { Count++; }
        }
    }
}
