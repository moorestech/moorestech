using System.Collections;
using System.IO;
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
        private sealed class CountingRequester : IPlaytestUploadRequester
        {
            internal int Count;
            public void RequestUpload() { Count++; }
        }
    }
}
