using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestUploadPathTest
    {
        [Test]
        public void prepareは箱の直下を指す()
        {
            Assert.AreEqual("report/bundle-1/prepare", PlaytestUploadPath.ForPrepare(PlaytestUploadKind.Report, "bundle-1"));
        }

        [Test]
        public void completeは箱の直下を指す()
        {
            Assert.AreEqual("report/bundle-1/complete", PlaytestUploadPath.ForComplete(PlaytestUploadKind.Report, "bundle-1"));
        }
    }
}
