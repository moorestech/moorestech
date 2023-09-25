using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.Chest;
using Core.Item;
using Game.Block.Interface;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util;
using Server.Protocol.PacketResponse.Util.InventoryMoveUitl;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util;

using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class InventoryItemMoveProtocolTest
    {
        private const int PlayerId = 0;
        private const int ChestBlockId = 7;
        
        [Test]
        public void MainInventoryMoveTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).GrabInventory;
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            
            //インベントリの設定
            mainInventory.SetItem(0,1,10);

            //インベントリを持っているアイテムに移す
            packet.GetPacketResponse(GetPacket(7,
                new FromItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory,0), new ToItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory,0)));
            
            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1,3), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(1,7), grabInventory.GetItem(0));
            
            
            
            //持っているアイテムをインベントリに移す
            packet.GetPacketResponse(GetPacket(5,
                new FromItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory,0), new ToItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory,0)));
            
            
            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1,8), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(1,2), grabInventory.GetItem(0));
        }

        [Test]
        public void CraftInventoryMoveTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).CraftingOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).GrabInventory;
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            //インベントリの設定
            craftInventory.SetItem(0,1,10);

            //インベントリを持っているアイテムに移す
            packet.GetPacketResponse(GetPacket(7,
                new FromItemMoveInventoryInfo(ItemMoveInventoryType.CraftInventory,0), new ToItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory,0)));
            
            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1,3), craftInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(1,7), grabInventory.GetItem(0));
            
            
            
            //持っているアイテムをインベントリに移す
            packet.GetPacketResponse(GetPacket(5,
                new FromItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory,0), new ToItemMoveInventoryInfo(ItemMoveInventoryType.CraftInventory,0)));
            
            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1,8), craftInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(1,2), grabInventory.GetItem(0));
        }

        [Test]
        public void BlockInventoryTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).GrabInventory;
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var worldDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var chest = (VanillaChest) blockFactory.Create(ChestBlockId,1);
            worldDataStore.AddBlock(chest,5,10,BlockDirection.North);
            
            //ブロックインベントリの設定
            chest.SetItem(1,1,10);
            
            
            
            //インベントリを持っているアイテムに移す
            packet.GetPacketResponse(GetPacket(7,
                new FromItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory,1,5,10), new ToItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory,0)));
            
            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1,3), chest.GetItem(1));
            Assert.AreEqual(itemStackFactory.Create(1,7), grabInventory.GetItem(0));
            
            
            
            //持っているアイテムをインベントリに移す
            packet.GetPacketResponse(GetPacket(5,
                new FromItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory,0), new ToItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory,1,5,10)));
            
            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1,8), chest.GetItem(1));
            Assert.AreEqual(itemStackFactory.Create(1,2), grabInventory.GetItem(0));
        }

        [Test]
        public void MainInventoryInsertTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).CraftingOpenableInventory;
            
            //インベントリの設定
            mainInventory.SetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0),1,10);
            craftInventory.SetItem(0,1,10);
            craftInventory.SetItem(2,2,10);
            
            //クラフトからメインにid 1のアイテムを移す
            packet.GetPacketResponse(GetPacket(5,
                new FromItemMoveInventoryInfo(ItemMoveInventoryType.CraftInventory,0), new ToItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory),ItemMoveType.InsertSlot));
           
            Assert.AreEqual(15,mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0)).Count);
            Assert.AreEqual(5,craftInventory.GetItem(0).Count);
            
            
            //id 2のアイテムをクラフトからメインに移す
            packet.GetPacketResponse(GetPacket(10,
                new FromItemMoveInventoryInfo(ItemMoveInventoryType.CraftInventory,2), new ToItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory),ItemMoveType.InsertSlot));
            
            Assert.AreEqual(10,mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(1)).Count);
            Assert.AreEqual(0,craftInventory.GetItem(2).Count);
            
        }
        

        private List<byte> GetPacket(int count,FromItemMoveInventoryInfo from,ToItemMoveInventoryInfo to,ItemMoveType itemMoveType = ItemMoveType.SwapSlot)
        {
            return MessagePackSerializer.Serialize(
                new InventoryItemMoveProtocolMessagePack(PlayerId,count,itemMoveType,from,to)).ToList();
        }
    }
}