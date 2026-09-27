using System.Text.RegularExpressions;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PacketResponseCreatorUnboundGateTest
    {
        [Test]
        public void 未紐づけ接続の要求はログを残して無視するTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var payload = MessagePackSerializer.Serialize(new GetChallengeInfoProtocol.RequestChallengeMessagePack());

            // 接続元が未確定なら要求の種別と拒否理由を記録する
            // Record the request tag and rejection reason when the sender is unknown
            LogAssert.Expect(LogType.Warning, new Regex("未紐づけ.*tag:" + Regex.Escape(GetChallengeInfoProtocol.ProtocolTag)));
            var responses = packet.GetPacketResponse(payload, new PacketResponseContext(null));
            Assert.IsEmpty(responses);
        }

        [Test]
        public void 紐づけ済み接続の要求は処理されるTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var payload = MessagePackSerializer.Serialize(new GetChallengeInfoProtocol.RequestChallengeMessagePack());

            // 同じ要求が身元確定後には通常の応答を返す
            // The same request returns its normal response once the sender is known
            var responses = packet.GetPacketResponse(payload, BoundPacketContext.Bind(1));
            Assert.AreEqual(1, responses.Count);
            Assert.IsNotNull(MessagePackSerializer.Deserialize<GetChallengeInfoProtocol.ResponseChallengeInfoMessagePack>(responses[0]));
        }
    }
}
