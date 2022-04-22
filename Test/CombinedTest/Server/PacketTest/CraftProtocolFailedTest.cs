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
    public class CraftProtocolFailedTest
    {
        private const short PacketId = 14;
        private const int PlayerId = 1;
        
        /// <summary>
        /// 別のアイテムを持っていてクラフトできないテスト
        /// </summary>
        [Test]
        public void CanNotNormalCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).GrabInventory;

            //craftingInventoryにアイテムを入れる
            var item = itemStackFactory.Create(1, 2);
            craftInventory.SetItem(0,item);
            
            //持っているスロットに別のアイテムを入れる
            grabInventory.SetItem(0,itemStackFactory.Create(2, 2));
            
            
            //プロトコルでクラフト実行
            var payLoad = new List<byte>();
            payLoad.AddRange(ToByteList.Convert(PacketId));
            payLoad.AddRange(ToByteList.Convert(PlayerId));
            payLoad.Add(0);
            packet.GetPacketResponse(payLoad);
            
            
            
            //クラフトできていないことをチェックする
            Assert.AreEqual(item,craftInventory.GetItem(0));
        }
        
        /// <summary>
        /// 全てクラフトを実行した時にインベントリがいっぱいだとインベントリが満杯になり、クラフトスロットに材料が余るテスト
        /// </summary>
        [Test]
        public void AllCraftReminderTest()
        {
            
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            //クラフトインベントリの作成
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventoryにアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(1, 100));

            //メインインベントリに2つのスロットを開けてアイテムを入れる
            for (int i = 2; i < mainInventory.GetSlotSize(); i++)
            {
                mainInventory.SetItem(i,itemStackFactory.Create(2, 100));
            }
            
            
            
            //プロトコルで全てクラフト実行
            var payLoad = new List<byte>();
            payLoad.AddRange(ToByteList.Convert(PacketId));
            payLoad.AddRange(ToByteList.Convert(PlayerId));
            payLoad.Add(1);
            packet.GetPacketResponse(payLoad);
            
            
            //メインインベントリのクラフト結果のチェック
            Assert.AreEqual(itemStackFactory.Create(1, 100),mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(1, 60),mainInventory.GetItem(1));
            //クラフトインベントリのアイテムが減ったことをチェックする
            Assert.AreEqual(itemStackFactory.Create(1, 98),mainInventory.GetItem(0));
        }
        
        /// <summary>
        /// インベントリがいっぱいで全てクラフトを実行できないテスト
        /// </summary>
        [Test]
        public void AllCanNotCraftTest()
        {
            
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            //クラフトインベントリの作成
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventoryにアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(1, 100));

            //メインインベントリに全てのスロットにアイテムを入れる
            for (int i = 0; i < mainInventory.GetSlotSize(); i++)
            {
                mainInventory.SetItem(i,itemStackFactory.Create(2, 100));
            }
            
            
            //プロトコルで全てクラフト実行
            var payLoad = new List<byte>();
            payLoad.AddRange(ToByteList.Convert(PacketId));
            payLoad.AddRange(ToByteList.Convert(PlayerId));
            payLoad.Add(1);
            packet.GetPacketResponse(payLoad);
            
            
            //クラフトインベントリのアイテムが減っていないことをチェック
            Assert.AreEqual(itemStackFactory.Create(1, 100),mainInventory.GetItem(0));
        }

        
        /// <summary>
        /// インベントリがいっぱいで1スタックのクラフトを実行できないテスト
        /// </summary>
        [Test]
        public void OneCanNotStackCraftTest()
        {
            
        }
        
    }
}