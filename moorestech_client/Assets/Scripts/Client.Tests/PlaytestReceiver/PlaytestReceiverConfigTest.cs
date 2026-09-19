using Client.PlaytestReceiver;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestReceiverConfigTest
    {
        [Test]
        public void アイドル期限は署名付きURLの寿命より短く0より大きい()
        {
            // URLの寿命は受け口だけが使う値なので、クライアントは複製を持たず contract.json を直接読む
            // The URL lifetime is used only by the receiver, so the client keeps no copy and reads contract.json directly
            Assert.Greater(PlaytestReceiverConfig.UploadIdleTimeoutSeconds, 0);
            Assert.Less(PlaytestReceiverConfig.UploadIdleTimeoutSeconds, (int)PlaytestReceiverContractTest.ReadContract()["uploadUrlTtlSeconds"]);
        }
    }
}
