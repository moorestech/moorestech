using Client.Game.InGame.BugReport.Submit;
using Client.Starter.Playtest;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestBootUploadTriggerTest
    {
        [Test]
        public void 起動時に一度だけ送信を要求する()
        {
            var requester = new CountingRequester();
            new PlaytestBootUploadTrigger(requester).Start();
            Assert.AreEqual(1, requester.Count);
        }
        private sealed class CountingRequester : IPlaytestUploadRequester
        {
            internal int Count;
            public void RequestUpload() { Count++; }
        }
    }
}
