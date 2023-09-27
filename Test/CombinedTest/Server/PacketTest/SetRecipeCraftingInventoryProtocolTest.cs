using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Test.Module.TestMod;

#if NET6_0
namespace Test.CombinedTest.Server.PacketTest
{
    public class SetRecipeCraftingInventoryProtocolTest
    {

        /// <summary>
        /// クラフトインベントリにレシピを置くテスト
        /// </summary>
        [Test]
        public void CraftingRecipePlaceTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            var mainInventory = playerInventory.MainOpenableInventory;
            var grabInventory = playerInventory.GrabInventory;
            var craftInventory = playerInventory.CraftingOpenableInventory;
            
            mainInventory.SetItem(0,1,2);
            mainInventory.SetItem(1,2,3);
            craftInventory.SetItem(0,1,1);
            grabInventory.SetItem(0,1,1);
            
            //設置するレシピ
            var recipe = new ItemMessagePack[]
            {
                new(1,1),new(1,1),new(2,1),
                new(2,1),new(0,0),new(0,0),
                new(0,0),new(0,0),new(0,0)
            };
            //レシピの設置を送信
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new SetRecipeCraftingInventoryProtocolMessagePack(0,recipe)).ToList());
            
            //アイテムが綺麗に分散して配置されているかチェック
            Assert.AreEqual(1,craftInventory.GetItem(0).Id);
            Assert.AreEqual(2,craftInventory.GetItem(0).Count);
            
            Assert.AreEqual(1,craftInventory.GetItem(1).Id);
            Assert.AreEqual(2,craftInventory.GetItem(1).Count);
            
            Assert.AreEqual(2,craftInventory.GetItem(2).Id);
            Assert.AreEqual(2,craftInventory.GetItem(2).Count);
            
            Assert.AreEqual(2,craftInventory.GetItem(3).Id);
            Assert.AreEqual(1,craftInventory.GetItem(3).Count);
        }


        /// <summary>
        /// アイテムが足りなくても一部分だけ置かれるテスト
        /// </summary>
        [Test]
        public void CraftRecipeLackItemTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            var mainInventory = playerInventory.MainOpenableInventory;
            var craftInventory = playerInventory.CraftingOpenableInventory;
            
            mainInventory.SetItem(0,1,2);
            
            //設置するレシピ
            var recipe = new ItemMessagePack[]
            {
                new(1,1),new(1,1),new(2,1),
                new(2,1),new(0,0),new(0,0),
                new(0,0),new(0,0),new(0,0)
            };
            //レシピの設置を送信
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new SetRecipeCraftingInventoryProtocolMessagePack(0,recipe)).ToList());
            
            //足りている部分だけ置かれているテスト
            Assert.AreEqual(1,craftInventory.GetItem(0).Id);
            Assert.AreEqual(1,craftInventory.GetItem(0).Count);
            
            Assert.AreEqual(1,craftInventory.GetItem(1).Id);
            Assert.AreEqual(1,craftInventory.GetItem(1).Count);
            
            //足りない部分には置かれていないテスト
            Assert.AreEqual(0,craftInventory.GetItem(2).Id);
            
            Assert.AreEqual(0,craftInventory.GetItem(3).Id);
        }
    }
}
#endif