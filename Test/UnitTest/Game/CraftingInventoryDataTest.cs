using System;
using Core.Item;
using Core.Item.Config;
using Game.Craft;
using Game.Craft.Config;
using Game.Craft.Interface;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using PlayerInventory;
using PlayerInventory.Event;
using PlayerInventory.ItemManaged;

namespace Test.UnitTest.Game
{
    public class CraftingInventoryDataTest
    {
        private const int PlayerId = 0;
    
        [Test]
        public void GetCreatableItemTest()
        {
            ItemStackFactory itemStackFactory = new ItemStackFactory(new TestItemConfig());
            ICraftingConfig config = new TestCraftConfig(itemStackFactory);
            IIsCreatableJudgementService service = new IsCreatableJudgementService(config,itemStackFactory);
            
            var craftConfig = config.GetCraftingConfigList()[0];
            
            //craftingInventoryにアイテムを入れる
            var craftingInventory = new CraftInventoryData(PlayerId,new PlayerMainInventoryUpdateEvent(),itemStackFactory,service);
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
            ItemStackFactory itemStackFactory = new ItemStackFactory(new TestItemConfig());
            ICraftingConfig config = new TestCraftConfig(itemStackFactory);
            IIsCreatableJudgementService service = new IsCreatableJudgementService(config,itemStackFactory);
            
            var craftConfig = config.GetCraftingConfigList()[0];
            
            
            //craftingInventoryにアイテムを入れる
            var craftingInventory = new CraftInventoryData(PlayerId,new PlayerMainInventoryUpdateEvent(),itemStackFactory,service);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            //クラフト実行
            craftingInventory.Craft();
            
            //ResultSlotにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result,craftingInventory.GetItem(PlayerInventoryConst.CraftInventorySize - 1 ));
            
            //クラフトスロットからアイテムが消えているかチェック
            for (int i = 0; i < PlayerInventoryConst.CraftSlotSize; i++)
            {
                Assert.AreEqual(itemStackFactory.CreatEmpty(),craftingInventory.GetItem(i));
            }
        }

        //クラフトを実行したときにアイテムが余るテスト
        [Test]
        public void CraftRemainderItemTest()
        {
            ItemStackFactory itemStackFactory = new ItemStackFactory(new TestItemConfig());
            ICraftingConfig config = new TestCraftConfig(itemStackFactory);
            IIsCreatableJudgementService service = new IsCreatableJudgementService(config,itemStackFactory);

            var craftConfig = config.GetCraftingConfigList()[0];
            
            
            //craftingInventoryに1つ余分にアイテムを入れる
            var craftingInventory = new CraftInventoryData(PlayerId,new PlayerMainInventoryUpdateEvent(),itemStackFactory,service);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                var itemId = craftConfig.Items[i].Id;
                var itemCount = craftConfig.Items[i].Count;
                var setItem = itemStackFactory.Create(itemId,itemCount + 1);
                craftingInventory.SetItem(i,setItem);
            }
            
            //クラフト実行
            craftingInventory.Craft();
            
            //ResultSlotにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result,craftingInventory.GetItem(PlayerInventoryConst.CraftInventorySize - 1 ));
            
            //クラフトスロットにアイテムが1つ残っているかチェック
            for (int i = 0; i < PlayerInventoryConst.CraftSlotSize; i++)
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
            ItemStackFactory itemStackFactory = new ItemStackFactory(new TestItemConfig());
            ICraftingConfig config = new TestCraftConfig(itemStackFactory);
            IIsCreatableJudgementService service = new IsCreatableJudgementService(config,itemStackFactory);

            var craftingInventory = new CraftInventoryData(PlayerId,new PlayerMainInventoryUpdateEvent(),itemStackFactory,service);
            
            //クラフト結果が何もないことをチェック
            Assert.AreEqual(itemStackFactory.CreatEmpty(),craftingInventory.GetCreatableItem());
            
            //クラフトしても出力スロットに何もないテスト
            craftingInventory.Craft();
            Assert.AreEqual(itemStackFactory.CreatEmpty(),craftingInventory.GetItem(PlayerInventoryConst.CraftInventorySize - 1));
            
        }
        
        
        //出力スロットにアイテムを入れれない時のテスト
        [Test]
        public void CanNotInsertOutputSlotToCanNotCraftTest()
        {
            //初期セットアップ
            var itemConfig = new TestItemConfig();
            ItemStackFactory itemStackFactory = new ItemStackFactory(itemConfig);
            ICraftingConfig config = new TestCraftConfig(itemStackFactory);
            IIsCreatableJudgementService service = new IsCreatableJudgementService(config,itemStackFactory);
            
            var craftConfig = config.GetCraftingConfigList()[0];
            var resultId = craftConfig.Result.Id;
            
            
            //craftingInventoryにアイテムを入れる
            var craftingInventory = new CraftInventoryData(PlayerId,new PlayerMainInventoryUpdateEvent(),itemStackFactory,service);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            
            
            //すでに別のアイテムがあってクラフトできないテスト
            //出力スロットに他の別のアイテムを入れる
            var setItem = itemStackFactory.Create(resultId + 1, 1);
            craftingInventory.SetItem(PlayerInventoryConst.CraftInventorySize - 1, setItem);
            
            //クラフト実行
            craftingInventory.Craft();
            
            //出力スロットのアイテムが変わっていないかチェック
            Assert.AreEqual(setItem,craftingInventory.GetItem(PlayerInventoryConst.CraftInventorySize - 1));
            //クラフトのスロットが変わっていないことをチェック
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                Assert.AreEqual(craftConfig.Items[i], craftingInventory.GetItem(i));
            }
            
            
            
            
            //すでにアイテムが満杯である時はクラフトできないテスト
            //出力スロットにアイテムを入れる
            setItem = itemStackFactory.Create(resultId,itemConfig.GetItemConfig(resultId).MaxStack);
            craftingInventory.SetItem(PlayerInventoryConst.CraftInventorySize - 1, setItem);
            
            //出力スロットのアイテムが変わっていないかチェック
            Assert.AreEqual(setItem,craftingInventory.GetItem(PlayerInventoryConst.CraftInventorySize - 1));
            //クラフトのスロットが変わっていないことをチェック
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                Assert.AreEqual(craftConfig.Items[i], craftingInventory.GetItem(i));
            }
        }
    }
}