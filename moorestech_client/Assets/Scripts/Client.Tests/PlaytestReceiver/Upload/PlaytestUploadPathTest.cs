using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestUploadPathTest
    {
        [Test]
        public void 階層はそのままで各セグメントだけがエスケープされる()
        {
            var path = PlaytestUploadPath.ForFile(PlaytestUploadKind.Report, "bundle-1", "logs/unity.log");

            Assert.AreEqual("report/bundle-1/logs/unity.log", path);
        }

        [Test]
        public void URLの意味を持つ文字を含む名前が別キーへ化けない()
        {
            // #はフラグメント・?はクエリとして切り落とされ、受け口には短いキーで保存されてしまう
            // A # is cut off as a fragment and a ? as a query, so the receiver would store a silently shorter key
            var path = PlaytestUploadPath.ForFile(PlaytestUploadKind.Report, "bundle-1", "snapshots/shot#1 a?b.png");

            Assert.AreEqual("report/bundle-1/snapshots/shot%231%20a%3Fb.png", path);
        }

        [Test]
        public void Windowsの区切りは送信前にスラッシュへ揃う()
        {
            var path = PlaytestUploadPath.ForFile(PlaytestUploadKind.Progress, "bundle-1", "world\\save.json");

            Assert.AreEqual("progress/bundle-1/world/save.json", path);
        }

        [Test]
        public void 逸脱を含む相対パスは組み立てない()
        {
            // 受け口は「..」を400で弾く。送る前にnullで返し、到達失敗と取り違えられないようにする
            // The receiver rejects ".." with a 400, so it returns null before sending and cannot look unreachable
            Assert.IsNull(PlaytestUploadPath.ForFile(PlaytestUploadKind.Report, "bundle-1", "../etc/passwd"));
            Assert.IsNull(PlaytestUploadPath.ForFile(PlaytestUploadKind.Report, "bundle-1", "logs//unity.log"));
            Assert.IsNull(PlaytestUploadPath.ForFile(PlaytestUploadKind.Report, "bundle-1", string.Empty));
        }

        [Test]
        public void completeは箱の直下を指す()
        {
            Assert.AreEqual("report/bundle-1/complete", PlaytestUploadPath.ForComplete(PlaytestUploadKind.Report, "bundle-1"));
        }
    }
}
