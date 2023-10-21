#if NET6_0
using System;
using System.Linq;
using Core.Const;
using Core.Item;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class PlayerInventoryProtocolTest
    {
        [Test]
        public void GetPlayerInventoryProtocolTest()
        {
            var playerId = 1;

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);


            
            var payload = MessagePackSerializer.Serialize(new RequestPlayerInventoryProtocolMessagePack(playerId)).ToList();
            
            var data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(packet.GetPacketResponse(payload)[0].ToArray());
            Assert.AreEqual(playerId, data.PlayerId);

            
            for (var i = 0; i < PlayerInventoryConst.MainInventoryColumns; i++)
            {
                Assert.AreEqual(ItemConst.EmptyItemId, data.Main[i].Id);
                Assert.AreEqual(0, data.Main[i].Count);
            }

            
            Assert.AreEqual(0, data.Grab.Id);
            Assert.AreEqual(0, data.Grab.Count);

            
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                Assert.AreEqual(ItemConst.EmptyItemId, data.Craft[i].Id);
                Assert.AreEqual(0, data.Craft[i].Count);
            }

            
            Assert.AreEqual(ItemConst.EmptyItemId, data.CraftResult.Id);
            Assert.AreEqual(0, data.CraftResult.Count);
            
            Assert.AreEqual(false, data.IsCreatable);


            
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            playerInventoryData.MainOpenableInventory.SetItem(0, itemStackFactory.Create(1, 5));
            playerInventoryData.MainOpenableInventory.SetItem(20, itemStackFactory.Create(3, 1));
            playerInventoryData.MainOpenableInventory.SetItem(34, itemStackFactory.Create(10, 7));


            
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[0];
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++)
            {
                var id = craftConfig.CraftItemInfos[i].ItemStack.Id;
                var count = craftConfig.CraftItemInfos[i].ItemStack.Count;
                Console.WriteLine(craftConfig.CraftItemInfos[i].ItemStack.Id);
                Console.WriteLine(craftConfig.CraftItemInfos[i].ItemStack.Count);
                playerInventoryData.CraftingOpenableInventory.SetItem(i, id, count * 2);
            }

            ;

            //　
            playerInventoryData.CraftingOpenableInventory.NormalCraft();


            //2
            data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(packet.GetPacketResponse(payload)[0].ToArray());
            Assert.AreEqual(playerId, data.PlayerId);

            
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
                if (i == 0)
                {
                    Assert.AreEqual(1, data.Main[i].Id);
                    Assert.AreEqual(5, data.Main[i].Count);
                }
                else if (i == 20)
                {
                    Assert.AreEqual(3, data.Main[i].Id);
                    Assert.AreEqual(1, data.Main[i].Count);
                }
                else if (i == 34)
                {
                    Assert.AreEqual(10, data.Main[i].Id);
                    Assert.AreEqual(7, data.Main[i].Count);
                }
                else
                {
                    Assert.AreEqual(ItemConst.EmptyItemId, data.Main[i].Id);
                    Assert.AreEqual(0, data.Main[i].Count);
                }

            
            
            Assert.AreEqual(craftConfig.Result.Id, data.Grab.Id);
            Assert.AreEqual(craftConfig.Result.Count, data.Grab.Count);


            
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                Assert.AreEqual(craftConfig.CraftItemInfos[i].ItemStack.Id, data.Craft[i].Id);
                Assert.AreEqual(craftConfig.CraftItemInfos[i].ItemStack.Count, data.Craft[i].Count);
            }

            
            Assert.AreEqual(true, data.IsCreatable);

            
            Assert.AreEqual(craftConfig.Result.Id, data.CraftResult.Id);
            Assert.AreEqual(craftConfig.Result.Count, data.CraftResult.Count);
        }
    }
}
#endif