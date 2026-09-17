using Client.PlaytestReceiver;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestReceiverConfigTest
    {
        [Test]
        public void 小さいファイルの期限は基礎の60秒()
        {
            Assert.AreEqual(PlaytestReceiverConfig.HttpTimeoutSeconds, PlaytestReceiverConfig.UploadTimeout(1024).TotalSeconds);
        }

        [Test]
        public void 大きいファイルはサイズに比例して期限が伸びる()
        {
            var tenMegaBytes = 10L * 1024 * 1024;
            var expected = PlaytestReceiverConfig.HttpTimeoutSeconds + tenMegaBytes / PlaytestReceiverConfig.UploadBytesPerSecondBudget;

            Assert.AreEqual(expected, PlaytestReceiverConfig.UploadTimeout(tenMegaBytes).TotalSeconds);
        }

        [Test]
        public void 期限には上限がある()
        {
            Assert.AreEqual(PlaytestReceiverConfig.MaxUploadTimeoutSeconds, PlaytestReceiverConfig.UploadTimeout(PlaytestReceiverConfig.MaxFileBytes * 10).TotalSeconds);
        }
    }
}
