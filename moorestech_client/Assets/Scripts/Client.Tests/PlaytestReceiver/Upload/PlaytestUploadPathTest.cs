using Client.PlaytestReceiver;
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

        [Test]
        public void 逸脱や区切り文字や制御文字を含むパスはunsafe_pathで拒む()
        {
            // 受け口の keys.ts isSafeSegment と同じ規則。宣言に混ぜると prepare 全体が400になる
            // The same rule as the receiver's isSafeSegment; mixing one into the declaration would 400 the whole prepare
            Assert.AreEqual("unsafe-path", Reject("../etc/passwd", 1));
            Assert.AreEqual("unsafe-path", Reject("logs//unity.log", 1));
            Assert.AreEqual("unsafe-path", Reject("logs/./unity.log", 1));
            Assert.AreEqual("unsafe-path", Reject(string.Empty, 1));
            Assert.AreEqual("unsafe-path", Reject("a\\b.log", 1));
            Assert.AreEqual("unsafe-path", Reject("tab" + (char)0x09 + "here.log", 1));
            Assert.AreEqual("unsafe-path", Reject("del" + (char)0x7f + ".log", 1));
        }

        [Test]
        public void 先頭セグメントが予約名ならreserved_nameで拒み中身の階層なら通す()
        {
            Assert.AreEqual("reserved-name", Reject("ACKED", 1));
            Assert.AreEqual("reserved-name", Reject("prepare/x.bin", 1));
            Assert.IsNull(Reject("logs/READY", 1));
        }

        [Test]
        public void 上限を超える大きさや件数や総量はそれぞれの理由で拒む()
        {
            Assert.AreEqual("too-large", Reject("video.mp4", PlaytestReceiverConfig.MaxFileBytes + 1));
            Assert.IsNull(Reject("video.mp4", PlaytestReceiverConfig.MaxFileBytes));
            Assert.AreEqual("too-many-files", PlaytestUploadPath.DescribeRejection("a.bin", 1, PlaytestReceiverConfig.MaxBundleFiles, 0));
            Assert.IsNull(PlaytestUploadPath.DescribeRejection("a.bin", 1, PlaytestReceiverConfig.MaxBundleFiles - 1, 0));
            Assert.AreEqual("bundle-too-large", PlaytestUploadPath.DescribeRejection("a.bin", 2, 1, PlaytestReceiverConfig.MaxBundleBytes - 1));
            Assert.IsNull(PlaytestUploadPath.DescribeRejection("a.bin", 1, 1, PlaytestReceiverConfig.MaxBundleBytes - 1));
        }

        [Test]
        public void 日本語や空白を含む名前は通す()
        {
            Assert.IsNull(Reject("ログ/ユニティ 1.log", 1));
        }

        private static string Reject(string relative, long bytes)
        {
            return PlaytestUploadPath.DescribeRejection(relative, bytes, 0, 0);
        }
    }
}
