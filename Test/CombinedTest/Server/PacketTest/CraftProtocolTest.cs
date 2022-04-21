using System.Collections.Generic;
using Core.Item;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;
using Test.Module.TestConfig;

namespace Test.CombinedTest.Server.PacketTest
{
    public class CraftProtocolTest
    {
        private const short PacketId = 14;
        private const int PlayerId = 1;
        
        [Test]
        public void CraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            //クラフトインベントリの作成
            var craftInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            
            //CraftConfigの作成
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[0];
            
            //craftingInventoryにアイテムを入れる
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            
            
            //プロトコルでクラフト実行
            var payLoad = new List<byte>();
            payLoad.AddRange(ToByteList.Convert(PacketId));
            payLoad.AddRange(ToByteList.Convert(PlayerId));
            payLoad.Add(0);
            packet.GetPacketResponse(payLoad);
            
            
            //クラフト結果がResultSlotにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result,craftInventory.GetItem(PlayerInventoryConst.CraftingInventorySize - 1 ));
        }

        [Test]
        public void AllCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[3]; //id3のレシピはこのテスト用のレシピ
            
            //craftingInventoryに2つ分のアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(1,2));
            
            
            
            //プロトコルでクラフト実行
            var payLoad = new List<byte>();
            payLoad.AddRange(ToByteList.Convert(PacketId));
            payLoad.AddRange(ToByteList.Convert(PlayerId));
            payLoad.Add(1);
            packet.GetPacketResponse(payLoad);
            
            
            //クラフト結果がメインインベントリにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result.Id,mainInventory.GetItem(0 ).Id);
            Assert.AreEqual(100,mainInventory.GetItem(0 ).Count);
            Assert.AreEqual(60,mainInventory.GetItem(1 ).Count);
        }

        [Test]
        public void OneStackCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[3]; //id3のレシピはこのテスト用のレシピ
            
            //craftingInventoryに2つ分のアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(1,2));
            
            
            
            //プロトコルでクラフト実行
            var payLoad = new List<byte>();
            payLoad.AddRange(ToByteList.Convert(PacketId));
            payLoad.AddRange(ToByteList.Convert(PlayerId));
            payLoad.Add(2);
            packet.GetPacketResponse(payLoad);
            
            
            //クラフト結果がメインインベントリにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result.Id,mainInventory.GetItem(0 ).Id);
            Assert.AreEqual(80,mainInventory.GetItem(0 ).Count);
        }
    }
}