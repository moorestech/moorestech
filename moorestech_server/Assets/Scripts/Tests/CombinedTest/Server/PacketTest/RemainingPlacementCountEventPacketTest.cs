using System.Linq;
using Game.Construction;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemainingPlacementCountEventPacketTest
    {
        private const int PlayerId = 1;

        [Test]
        public void 残り設置数の変更が該当プレイヤーへイベント配信される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;

            RemainingPlacementCountTestState.SetRemainingCount(serviceProvider, PlayerId, wallet, 2);
            serviceProvider.GetService<IRemainingPlacementCountMutation>().FlushChanges();

            var events = sink.TakeAll().Where(e => e.Tag == RemainingPlacementCountChangedEventPacket.EventTag).ToList();
            Assert.AreEqual(1, events.Count);
            var data = MessagePackSerializer.Deserialize<RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack>(events[0].Payload);
            Assert.AreEqual(wallet.AsPrimitive(), data.WalletBlockId);
            Assert.AreEqual(2, data.RemainingCount);
        }

        [Test]
        public void 初期ハンドシェイクに残り設置数が同梱される()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            RemainingPlacementCountTestState.SetRemainingCount(serviceProvider, PlayerId, wallet, 2);

            var payload = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(PlayerId, "test"));
            var responseBytes = packet.GetPacketResponse(payload, new PacketResponseContext(null))[0];
            var response = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(responseBytes);

            Assert.AreEqual(1, response.RemainingPlacementCounts.Length);
            Assert.AreEqual(wallet.AsPrimitive(), response.RemainingPlacementCounts[0].WalletBlockId);
            Assert.AreEqual(2, response.RemainingPlacementCounts[0].RemainingCount);
        }
    }
}
