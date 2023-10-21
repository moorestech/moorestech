#if NET6_0
using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class CraftInventoryUpdateTest
    {
        private const int PlayerId = 1;

        [Test]
        public void CraftEventTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            
            //craftingInventory
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[0];
            var craftingInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++) craftingInventory.SetItem(i, craftConfig.CraftItemInfos[i].ItemStack);

            
            var response = packetResponse.GetPacketResponse(EventRequest());

            const int craftEventCount = 6;

            Assert.AreEqual(craftEventCount, response.Count); 


            
            var checkSlot = craftEventCount - 2;


            var data = MessagePackSerializer.Deserialize<CraftingInventoryUpdateEventMessagePack>(response[checkSlot].ToArray());

            Assert.AreEqual(7, data.Slot); // 
            Assert.AreEqual(craftConfig.CraftItemInfos[7].ItemStack.Id, data.Item.Id); //ID
            Assert.AreEqual(craftConfig.CraftItemInfos[7].ItemStack.Count, data.Item.Count); 

            Assert.AreEqual(ItemConst.EmptyItemId, data.CreatableItem.Id); //ID
            Assert.AreEqual(0, data.CreatableItem.Count); 
            Assert.AreEqual(false, data.IsCraftable); //0


            
            checkSlot = craftEventCount - 1;
            data = MessagePackSerializer.Deserialize<CraftingInventoryUpdateEventMessagePack>(response[checkSlot].ToArray());
            Assert.AreEqual(8, data.Slot); // 
            Assert.AreEqual(craftConfig.CraftItemInfos[8].ItemStack.Id, data.Item.Id); //ID
            Assert.AreEqual(craftConfig.CraftItemInfos[8].ItemStack.Count, data.Item.Count); 

            Assert.AreEqual(craftConfig.Result.Id, data.CreatableItem.Id); //ID
            Assert.AreEqual(craftConfig.Result.Count, data.CreatableItem.Count); 
            Assert.AreEqual(true, data.IsCraftable); //1


            
            craftingInventory.NormalCraft();


            
            response = packetResponse.GetPacketResponse(EventRequest());
            Assert.AreEqual(craftEventCount + 1, response.Count); //craftEventCount + 1

            
            checkSlot = craftEventCount + 1 - 1;
            var grabData = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(response[checkSlot].ToArray());

            Assert.AreEqual(craftConfig.Result.Id, grabData.Item.Id); //ID
            Assert.AreEqual(craftConfig.Result.Count, grabData.Item.Count); 
        }

        


        private List<byte> EventRequest()
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(PlayerId)).ToList();
        }
    }
}
#endif