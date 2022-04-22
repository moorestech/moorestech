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
            
        }
        
        /// <summary>
        /// インベントリがいっぱいでクラフトを実行できないテスト
        /// </summary>
        [Test]
        public void AllCanNotCraftTest()
        {
            
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