using Core.Item;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using PlayerInventory;
using PlayerInventory.Event;

namespace Test.UnitTest.Game
{
    public class CraftingInventoryDataTest
    {
        private const int PlayerId = 0;
        public void GetResultTest()
        {
            ICraftingConfig config = null;
            ItemStackFactory itemStackFactory = null;
            
            var craftConfig = config.GetCraftingConfig()[0];
            
            //craftingInventoryにアイテムを入れる
            var craftingInventory = new PlayerCraftingInventoryData(PlayerId,new PlayerInventoryUpdateEvent(),itemStackFactory);
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            //インベントリのconfigからデータを取得
            //getResultを検証する
            Assert.AreEqual(craftConfig.Result,craftingInventory.GetResult());
        }

        public void CraftTest()
        {
            ICraftingConfig config = null;
            ItemStackFactory itemStackFactory = null;

            var craftConfig = config.GetCraftingConfig()[0];
            
            
            //craftingInventoryにアイテムを入れる
            var craftingInventory = new PlayerCraftingInventoryData(PlayerId,new PlayerInventoryUpdateEvent(),itemStackFactory);
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
        public void CraftRemainderItemTest()
        {
            
            ICraftingConfig config = null;
            ItemStackFactory itemStackFactory = null;

            var craftConfig = config.GetCraftingConfig()[0];
            
            
            //craftingInventoryに1つ余分にアイテムを入れる
            var craftingInventory = new PlayerCraftingInventoryData(PlayerId,new PlayerInventoryUpdateEvent(),itemStackFactory);
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