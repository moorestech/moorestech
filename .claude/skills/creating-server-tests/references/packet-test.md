# パケットテストパターン

```csharp
using MessagePack;
using Server.Boot;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class {Protocol}Test
    {
        [Test]
        public void {テスト内容}Test()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // リクエスト送信
            var requestData = MessagePackSerializer.Serialize(new RequestMessage(...));

            // レスポンス取得
            var responses = packet.GetPacketResponse(requestData);

            // デシリアライズして検証
            var response = MessagePackSerializer.Deserialize<ResponseMessage>(responses[0].ToArray());
            Assert.AreEqual(expected, response.Value);
        }
    }
}
```
