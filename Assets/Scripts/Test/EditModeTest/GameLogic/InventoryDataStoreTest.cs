using System.Collections.Generic;
using System.Reflection;
using MainGame.Basic;
using MainGame.GameLogic.Inventory;
using MainGame.Network.Event;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.UI.Inventory.View;
using NUnit.Framework;
using UnityEngine;

namespace Test.EditModeTest.GameLogic
{
    public class InventoryDataStoreTest
    {
        [Test]
        public void SetInventoryTest()
        {
            var inventoryEvent = new PlayerInventoryUpdateEvent();
            var playerInventoryItemView = new GameObject().AddComponent<PlayerInventoryItemView>();
            var inventoryDataStore = new PlayerInventoryDataCache(inventoryEvent,playerInventoryItemView);
            
            //インベントリの設定
            var inventory = new List<ItemStack>();
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                inventory.Add(new ItemStack());
            }
            
            //インベントリをセットされているテスト
            inventoryEvent.OnOnPlayerInventoryUpdateEvent(
                new OnPlayerInventoryUpdateProperties(10,inventory));

            var inventoryItems = (List<ItemStack>)typeof(PlayerInventoryDataCache).GetField("_items",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inventoryDataStore);
            
            //アイテムの検証
            Assert.AreEqual(PlayerInventoryConstant.MainInventorySize,inventoryItems.Count);
            foreach (var inventoryItem in inventoryItems)
            {
                Assert.AreEqual(0,inventoryItem.Count);
                Assert.AreEqual(0,inventoryItem.ID);
            }
            
            
            //スロットのアップデートのテスト
            inventoryEvent.OnOnPlayerInventorySlotUpdateEvent(new OnPlayerInventorySlotUpdateProperties(
                0,new ItemStack(1,5)));
            //検証
            Assert.AreEqual(1,inventoryItems[0].ID);
            Assert.AreEqual(5,inventoryItems[0].Count);
            
            inventoryEvent.OnOnPlayerInventorySlotUpdateEvent(new OnPlayerInventorySlotUpdateProperties(
                3,new ItemStack(5,2)));
            Assert.AreEqual(5,inventoryItems[3].ID);
            Assert.AreEqual(2,inventoryItems[3].Count);
            
            
            inventoryEvent.OnOnPlayerInventorySlotUpdateEvent(new OnPlayerInventorySlotUpdateProperties(
                30,new ItemStack(12,23)));
            Assert.AreEqual(12,inventoryItems[30].ID);
            Assert.AreEqual(23,inventoryItems[30].Count);
            
            inventoryEvent.OnOnPlayerInventorySlotUpdateEvent(new OnPlayerInventorySlotUpdateProperties(
                PlayerInventoryConstant.MainInventorySize-1,new ItemStack(30,10)));
            Assert.AreEqual(30,inventoryItems[PlayerInventoryConstant.MainInventorySize-1].ID);
            Assert.AreEqual(10,inventoryItems[PlayerInventoryConstant.MainInventorySize-1].Count);
            
        }
    }
}