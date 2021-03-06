using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;
using Server.Protocol.PacketResponse;

using Server.Util;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class CraftProtocolFailedTest
    {
        private const int PlayerId = 1;
        
        private const int TestCraftItemId = 6;
        
        /// <summary>
        /// 別のアイテムを持っていてクラフトできないテスト
        /// </summary>
        [Test]
        public void CanNotNormalCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).GrabInventory;

            //craftingInventoryにアイテムを入れる
            var item = itemStackFactory.Create(TestCraftItemId, 2);
            craftInventory.SetItem(0,item);
            
            //持っているスロットに別のアイテムを入れる
            grabInventory.SetItem(0,itemStackFactory.Create(2, 1));
            
            
            //プロトコルでクラフト実行
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId,0)).ToList());
            
            
            
            //クラフトできていないことをチェックする
            Assert.AreEqual(item,craftInventory.GetItem(0));
        }
        
        /// <summary>
        /// 全てクラフトを実行した時にインベントリがいっぱいだとインベントリが満杯になり、クラフトスロットに材料が余るテスト
        /// </summary>
        [Test]
        public void AllCraftReminderTest()
        {
            
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            //クラフトインベントリの作成
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventoryにアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(TestCraftItemId, 100));

            //メインインベントリに2つのスロットを開けてアイテムを入れる
            for (int i = 2; i < mainInventory.GetSlotSize(); i++)
            {
                mainInventory.SetItem(i,itemStackFactory.Create(2, 1));
            }
            
            
            
            //プロトコルで全てクラフト実行
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId,1)).ToList());
            
            
            //メインインベントリのクラフト結果のチェック
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 100),mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 60),mainInventory.GetItem(1));
            //クラフトインベントリのアイテムが減ったことをチェックする
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 98),craftInventory.GetItem(0));
        }
        
        /// <summary>
        /// インベントリがいっぱいで全てクラフトを実行できないテスト
        /// </summary>
        [Test]
        public void AllCanNotCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            //クラフトインベントリの作成
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventoryにアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(TestCraftItemId, 100));

            //メインインベントリに全てのスロットにアイテムを入れる
            for (int i = 0; i < mainInventory.GetSlotSize(); i++)
            {
                mainInventory.SetItem(i,itemStackFactory.Create(2, 1));
            }
            
            
            //プロトコルで全てクラフト実行
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId,1)).ToList());
            
            
            //クラフトインベントリのアイテムが減っていないことをチェック
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 100),craftInventory.GetItem(0));
        }

        
        /// <summary>
        /// 1スタッククラフトを実行した時にインベントリがいっぱいだとインベントリが満杯になり、クラフトスロットに材料が余るテスト
        /// </summary>
        [Test]
        public void OneStackCraftReminderTest(){
        
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            //クラフトインベントリの作成
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventoryにアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(TestCraftItemId, 100));

            //メインインベントリに1つのスロットを開けてアイテムを入れる
            for (int i = 1; i < mainInventory.GetSlotSize(); i++)
            {
                mainInventory.SetItem(i,itemStackFactory.Create(2, 1));
            }
            //1回だけクラフトできる量をメインインベントリに入れておく
            mainInventory.SetItem(0,itemStackFactory.Create(TestCraftItemId, 20));
            
            
            
            //プロトコルで1スタッククラフト実行
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId,2)).ToList());
            
            
            //メインインベントリのクラフト結果のチェック
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 100),mainInventory.GetItem(0));
            //クラフトインベントリのアイテムが減ったことをチェックする
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 99),craftInventory.GetItem(0));
        }
        
        
        /// <summary>
        /// インベントリがいっぱいで1スタックのクラフトを実行できないテスト
        /// </summary>
        [Test]
        public void OneCanNotStackCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            //クラフトインベントリの作成
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventoryにアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(TestCraftItemId, 100));

            //メインインベントリに全てのスロットにアイテムを入れる
            for (int i = 0; i < mainInventory.GetSlotSize(); i++)
            {
                mainInventory.SetItem(i,itemStackFactory.Create(2, 1));
            }
            
            
            //プロトコルで一スタッククラフト実行
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId,2)).ToList());
            
            
            //クラフトインベントリのアイテムが減っていないことをチェック
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 100),craftInventory.GetItem(0));
        }
        
    }
}