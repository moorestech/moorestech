using Client.PlaytestReceiver;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestReceiverConfigTest
    {
        [Test]
        public void アイドル期限はトークン寿命より短く0より大きい()
        {
            Assert.Greater(PlaytestReceiverConfig.UploadIdleTimeoutSeconds, 0);
            Assert.Less(PlaytestReceiverConfig.UploadIdleTimeoutSeconds, PlaytestReceiverConfig.UploadUrlTtlSeconds);
        }
    }
}
