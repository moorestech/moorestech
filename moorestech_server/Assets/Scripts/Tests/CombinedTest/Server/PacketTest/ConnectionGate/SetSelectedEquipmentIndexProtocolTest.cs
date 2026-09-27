using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class SetSelectedEquipmentIndexProtocolTest
    {
        [Test]
        public void 接続に紐づいたプレイヤーの装備選択だけが変わるTest()
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 異なる身元を接続し、同じ要求を一人目から送る
            // Connect distinct identities and send the identity-free request as the first player
            var contextA = BoundPacketContext.Handshake(packet, "steam:1", out var playerA);
            BoundPacketContext.Handshake(packet, "steam:2", out var playerB);
            var request = new SetSelectedEquipmentIndexProtocol.SetSelectedEquipmentIndexMessagePack(1);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(request), contextA);

            // 装備選択は送り手だけに反映される
            // Equipment selection changes only for the bound sender
            var store = provider.GetRequiredService<IPlayerInventoryDataStore>();
            Assert.AreEqual(1, store.GetInventoryData(playerA).EquipmentInventory.SelectedEquipmentIndex);
            Assert.AreEqual(0, store.GetInventoryData(playerB).EquipmentInventory.SelectedEquipmentIndex);
        }
    }
}
