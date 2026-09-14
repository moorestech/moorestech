using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestUploadPathTest
    {
        [Test]
        public void 階層はそのままで各セグメントだけがエスケープされる()
        {
            var path = PlaytestUploadPath.ForFile("bug-report", "bundle-1", "logs/unity.log");

            Assert.AreEqual("bug-report/bundle-1/logs/unity.log", path);
        }

        [Test]
        public void URLの意味を持つ文字を含む名前が別キーへ化けない()
        {
            // #はフラグメント・?はクエリとして切り落とされ、受け口には短いキーで保存されてしまう
            // A # is cut off as a fragment and a ? as a query, so the receiver would store a silently shorter key
            var path = PlaytestUploadPath.ForFile("bug-report", "bundle-1", "snapshots/shot#1 a?b.png");

            Assert.AreEqual("bug-report/bundle-1/snapshots/shot%231%20a%3Fb.png", path);
        }

        [Test]
        public void Windowsの区切りは送信前にスラッシュへ揃う()
        {
            var path = PlaytestUploadPath.ForFile("bug-report", "bundle-1", "world\\save.json");

            Assert.AreEqual("bug-report/bundle-1/world/save.json", path);
        }

        [Test]
        public void completeは箱の直下を指す()
        {
            Assert.AreEqual("bug-report/bundle-1/complete", PlaytestUploadPath.ForComplete("bug-report", "bundle-1"));
        }
    }
}
