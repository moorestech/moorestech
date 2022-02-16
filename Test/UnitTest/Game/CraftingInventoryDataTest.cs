using Core.Item;
using Core.Item.Config;
using Game.Crafting.Config;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using PlayerInventory;
using PlayerInventory.Event;

namespace Test.UnitTest.Game
{
    public class CraftingInventoryDataTest
    {
        //TODO クラフトしたらアイテムが溢れる時にクラフトできないテスト
        //TODO アイテムがない時はクラフトできないテスト

        private const int PlayerId = 0;
    
        [Test]
        public void GetResultTest()
        {
            ItemStackFactory itemStackFactory = new ItemStackFactory(new TestItemConfig());
            ICraftingConfig config = new TestCraftConfig(itemStackFactory);
            IIsCreatableJudgementService service = null;
            
            var craftConfig = config.GetCraftingConfig()[0];
            
            //craftingInventoryにアイテムを入れる
            var craftingInventory = new PlayerCraftingInventoryData(PlayerId,new PlayerInventoryUpdateEvent(),itemStackFactory,service);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            //インベントリのconfigからデータを取得
            //getResultを検証する
            Assert.AreEqual(craftConfig.Result,craftingInventory.GetResult());
        }

        [Test]
        public void CraftTest()
        {
            ItemStackFactory itemStackFactory = new ItemStackFactory(new TestItemConfig());
            ICraftingConfig config = new TestCraftConfig(itemStackFactory);
            IIsCreatableJudgementService service = null;

            var craftConfig = config.GetCraftingConfig()[0];
            
            
            //craftingInventoryにアイテムを入れる
            var craftingInventory = new PlayerCraftingInventoryData(PlayerId,new PlayerInventoryUpdateEvent(),itemStackFactory,service);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            //クラフト実行
            craftingInventory.Craft();
            
            //ResultSlotにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result,craftingInventory.GetItem(PlayerInventoryConst.CraftingInventorySize - 1 ));
            
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
            ItemStackFactory itemStackFactory = new ItemStackFactory(new TestItemConfig());
            ICraftingConfig config = new TestCraftConfig(itemStackFactory);
            IIsCreatableJudgementService service = null;

            var craftConfig = config.GetCraftingConfig()[0];
            
            
            //craftingInventoryに1つ余分にアイテムを入れる
            var craftingInventory = new PlayerCraftingInventoryData(PlayerId,new PlayerInventoryUpdateEvent(),itemStackFactory,service);
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
            Assert.AreEqual(craftConfig.Result,craftingInventory.GetItem(PlayerInventoryConst.CraftingInventorySize - 1 ));
            
            //クラフトスロットにアイテムが1つ残っているかチェック
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var inventoryItem = craftingInventory.GetItem(i);
                
                Assert.AreEqual(craftConfig.Items[i].Id,inventoryItem.Id);//check id
                Assert.AreEqual(1,inventoryItem.Count);//check count
            }
        }
    }
}