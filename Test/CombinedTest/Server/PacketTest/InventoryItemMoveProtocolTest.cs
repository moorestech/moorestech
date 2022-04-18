using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;
using Test.Module.TestConfig;

namespace Test.CombinedTest.Server.PacketTest
{
    public class InventoryItemMoveProtocolTest
    {
        private int playerId = 0;
        
        [Test]
        public void MainInventoryMoveTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var equipmentInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).EquipmentInventory;
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            
            //インベントリの設定
            mainInventory.SetItem(0,1,10);

            //インベントリを持っているアイテムに移す
            packet.GetPacketResponse(GetPacket(true, 0, 0, 7));
            
            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1,3), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(1,7), equipmentInventory.GetItem(0));
        }

        [Test]
        public void CraftInventoryMoveTest()
        {
            
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).CraftingOpenableInventory;
            var equipmentInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).EquipmentInventory;
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            //インベントリの設定
            craftInventory.SetItem(0,1,10);

            //インベントリを持っているアイテムに移す
            packet.GetPacketResponse(GetPacket(true, 0, 0, 7));
            
            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(1,3), craftInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(1,7), equipmentInventory.GetItem(0));
        }

        //sourceInventoryIdは 0 メイン　1 クラフト 2 ブロック 
        private List<byte> GetPacket(bool toEquipment,byte sourceInventoryId,int inventorySlot,int itemCount,int x = 0,int y = 0)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 5));
            payload.Add(toEquipment ? (byte) 0 : (byte) 1);
            payload.Add(sourceInventoryId);
            payload.AddRange(ToByteList.Convert(playerId));
            payload.AddRange(ToByteList.Convert(inventorySlot));
            payload.AddRange(ToByteList.Convert(itemCount));
            payload.AddRange(ToByteList.Convert(x));
            payload.AddRange(ToByteList.Convert(y));

            return payload;
        }
        
        
    }
}