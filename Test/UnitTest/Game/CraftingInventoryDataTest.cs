using System;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Game.Crafting;
using Game.Crafting.Config;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using PlayerInventory.Event;
using PlayerInventory.ItemManaged;
using Server.Boot;
using Server.StartServerSystem;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.UnitTest.Game
{
    public class CraftingInventoryDataTest
    {
        private const int PlayerId = 0;
    
        [Test]
        public void GetCreatableItemTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();
            
            var craftConfig = config.GetCraftingConfigList()[0];
            
            //craftingInventoryにアイテムを入れる
            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grab = new GrabInventoryData(PlayerId, new GrabInventoryUpdateEvent(), itemStackFactory);
            var craftingInventory = new CraftingOpenableInventoryData(PlayerId,new CraftInventoryUpdateEvent(),itemStackFactory,service,main,grab);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            //インベントリのconfigからデータを取得
            //getCreatableItemを検証する
            Assert.AreEqual(craftConfig.Result,craftingInventory.GetCreatableItem());
        }

        [Test]
        public void CraftTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();
            
            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grabInventory = new GrabInventoryData(PlayerId,new GrabInventoryUpdateEvent(),itemStackFactory);

            
            
            var craftConfig = config.GetCraftingConfigList()[0];
            
            
            //craftingInventoryにアイテムを入れる
            var craftingInventory = new CraftingOpenableInventoryData(PlayerId,new CraftInventoryUpdateEvent(),itemStackFactory,service,main,grabInventory);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            //クラフト実行
            craftingInventory.NormalCraft();
            
            //grabInventoryにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result,grabInventory.GetItem(0));
            
            //クラフトスロットからアイテムが消えているかチェック
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                Assert.AreEqual(itemStackFactory.CreatEmpty(),craftingInventory.GetItem(i));
            }
        }

        //クラフトを実行したときにアイテムが余るテスト
        [Test]
        public void CraftRemainderItemTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();
            
            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grabInventory = new GrabInventoryData(PlayerId,new GrabInventoryUpdateEvent(),itemStackFactory);
            
            var craftConfig = config.GetCraftingConfigList()[0];
            
            
            //craftingInventoryに1つ余分にアイテムを入れる
            var craftingInventory = new CraftingOpenableInventoryData(PlayerId,new CraftInventoryUpdateEvent(),itemStackFactory,service,main,grabInventory);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                var itemId = craftConfig.Items[i].Id;
                var itemCount = craftConfig.Items[i].Count;
                var setItem = itemStackFactory.Create(itemId,itemCount + 1);
                craftingInventory.SetItem(i,setItem);
            }
            
            //クラフト実行
            craftingInventory.NormalCraft();
            
            //grabInventoryにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result,grabInventory.GetItem(0));
            
            //クラフトスロットにアイテムが1つ残っているかチェック
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var inventoryItem = craftingInventory.GetItem(i);
                
                Assert.AreEqual(craftConfig.Items[i].Id,inventoryItem.Id);//check id
                //クラフトの配置とにアイテムがある時のみチェック
                if (craftConfig.Items[i].Count != 0)
                {
                    Assert.AreEqual(1,inventoryItem.Count);//check count
                }
            }
        }

        [Test]
        //アイテムが足りないときはクラフトできないテスト
        public void NoneCraftSlotItemTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();
            
            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grabInventory = new GrabInventoryData(PlayerId,new GrabInventoryUpdateEvent(),itemStackFactory);
            

            var craftingInventory = new CraftingOpenableInventoryData(PlayerId,new CraftInventoryUpdateEvent(),itemStackFactory,service,main,grabInventory);

            
            //クラフトしてもgrabInventoryに何もないテスト
            craftingInventory.NormalCraft();
            Assert.AreEqual(itemStackFactory.CreatEmpty(),grabInventory.GetItem(0));
            
        }
        
        
        //出力スロットにアイテムを入れれない時のテスト
        [Test]
        public void CanNotInsertOutputSlotToCanNotCraftTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>(); 
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();
            
            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grabInventory = new GrabInventoryData(PlayerId,new GrabInventoryUpdateEvent(),itemStackFactory);
            
            
            
            
            var craftConfig = config.GetCraftingConfigList()[0];
            var resultId = craftConfig.Result.Id;
            
            
            //craftingInventoryにアイテムを入れる
            var craftingInventory = new CraftingOpenableInventoryData(PlayerId,new CraftInventoryUpdateEvent(),itemStackFactory,service,main,grabInventory);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            
            
            //すでに別のアイテムがあってクラフトできないテスト
            //出力スロットに他の別のアイテムを入れる
            var setItem = itemStackFactory.Create(resultId + 1, 1);
            grabInventory.SetItem(0, setItem);
            
            //クラフト実行
            craftingInventory.NormalCraft();
            
            //出力スロットのアイテムが変わっていないかチェック
            Assert.AreEqual(setItem,grabInventory.GetItem(0));
            //クラフトのスロットが変わっていないことをチェック
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                Assert.AreEqual(craftConfig.Items[i], craftingInventory.GetItem(i));
            }
            
            
            
            
            //すでにアイテムが満杯である時はクラフトできないテスト
            //出力スロットにアイテムを入れる
            setItem = itemStackFactory.Create(resultId,itemConfig.GetItemConfig(resultId).MaxStack);
            grabInventory.SetItem(0, setItem);

            //クラフト実行
            craftingInventory.NormalCraft();
            
            //出力スロットのアイテムが変わっていないかチェック
            Assert.AreEqual(setItem,grabInventory.GetItem(0));
            //クラフトのスロットが変わっていないことをチェック
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                Assert.AreEqual(craftConfig.Items[i], craftingInventory.GetItem(i));
            }
        }
    }
}