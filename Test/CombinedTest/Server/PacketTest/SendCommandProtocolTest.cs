#if NET6_0
using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class SendCommandProtocolTest
    {
        [Test]
        public void GiveCommandTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            
            //ID25
            var commandPacket = GetGiveCommandPacket(10, 2, 5);
            
            packet.GetPacketResponse(commandPacket);


            

            
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(10);

            //0
            var id2Slot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            Assert.AreEqual(2, playerInventory.MainOpenableInventory.GetItem(id2Slot).Id);
            Assert.AreEqual(5, playerInventory.MainOpenableInventory.GetItem(id2Slot).Count);


            //ID1
            packet.GetPacketResponse(GetGiveCommandPacket(10, 3, 7));
            var id3Slot = PlayerInventoryConst.HotBarSlotToInventorySlot(1);
            Assert.AreEqual(3, playerInventory.MainOpenableInventory.GetItem(id3Slot).Id);
            Assert.AreEqual(7, playerInventory.MainOpenableInventory.GetItem(id3Slot).Count);

            //ID20
            packet.GetPacketResponse(GetGiveCommandPacket(10, 2, 3));
            Assert.AreEqual(2, playerInventory.MainOpenableInventory.GetItem(id2Slot).Id);
            Assert.AreEqual(8, playerInventory.MainOpenableInventory.GetItem(id2Slot).Count);
        }

        private List<byte> GetGiveCommandPacket(int playerId, int itemId, int count)
        {
            var giveCommand = $"give {playerId} {itemId} {count}"; //give <playerId> <itemId> <count>


            return MessagePackSerializer.Serialize(new SendCommandProtocolMessagePack(giveCommand)).ToList();
        }
    }
}
#endif