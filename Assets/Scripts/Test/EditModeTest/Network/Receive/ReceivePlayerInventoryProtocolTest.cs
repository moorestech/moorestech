using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Receive;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;
using Test.TestModule.Util;

namespace Test.EditModeTest.Network.Receive
{
    public class ReceivePlayerInventoryProtocolTest
    {
        //ReceivePlayerInventoryProtocol単体のテスト
        [Test]
        public void ReceivedPacketToPlayerInventoryTest()
        {
            var inventoryEvent = new MainInventoryUpdateEvent();
            var protocol = new ReceivePlayerInventoryProtocol(inventoryEvent,new CraftingInventoryUpdateEvent());
            var inventoryDataStore = new TestPlayerInventoryDataStore(inventoryEvent);

            var playerId = 10;
            
            var setItems = new Dictionary<int,ItemStack>();
            //セットするアイテムを定義
            setItems.Add(0,new ItemStack(1,10));
            setItems.Add(1,new ItemStack(2,5));
            setItems.Add(24,new ItemStack(3,23));
            setItems.Add(PlayerInventoryConstant.MainInventorySize-1,new ItemStack(100,19));
            
            
            
            //パケットを解析
            protocol.Analysis(CreatePlayerInventoryPacket.Create(playerId,setItems));
            
            
            
            //検証
            Assert.True(inventoryDataStore.playerInventory.ContainsKey(playerId));
            Assert.AreEqual(1,inventoryDataStore.playerInventory.Count);
            //アイテムの検証
            var playerInventory = inventoryDataStore.playerInventory[playerId];
            for (int i = 0; i < playerInventory.Count; i++)
            {
                var id = playerInventory[i].ID;
                var count = playerInventory[i].Count;
                if (setItems.ContainsKey(i))
                {
                    Assert.AreEqual(setItems[i].ID,id);
                    Assert.AreEqual(setItems[i].Count,count);
                    continue;
                }
                Assert.AreEqual(ItemConstant.NullItemId,id);
                Assert.AreEqual(ItemConstant.NullItemCount,count);
            }
        }
        
        
        //AllReceivePacketAnalysisService経由のテスト
        [Test]
        public void ReceivedPacketToPlayerInventoryViaAllReceivePacketAnalysisServiceTestTest()
        {
            var inventoryEvent = new MainInventoryUpdateEvent();
            var protocol = new AllReceivePacketAnalysisService(new NetworkReceivedChunkDataEvent(),inventoryEvent,new CraftingInventoryUpdateEvent(),null);
            var inventoryDataStore = new TestPlayerInventoryDataStore(inventoryEvent);

            var playerId = 10;
            
            var setItems = new Dictionary<int,ItemStack>();
            //セットするアイテムを定義
            setItems.Add(0,new ItemStack(1,10));
            setItems.Add(1,new ItemStack(2,5));
            setItems.Add(24,new ItemStack(3,23));
            setItems.Add(PlayerInventoryConstant.MainInventorySize-1,new ItemStack(100,19));
            
            
            
            //パケットを解析
            protocol.Analysis(CreatePlayerInventoryPacket.Create(playerId,setItems).ToArray());
            
            
            
            //検証
            Assert.True(inventoryDataStore.playerInventory.ContainsKey(playerId));
            Assert.AreEqual(1,inventoryDataStore.playerInventory.Count);
            //アイテムの検証
            var playerInventory = inventoryDataStore.playerInventory[playerId];
            for (int i = 0; i < playerInventory.Count; i++)
            {
                var id = playerInventory[i].ID;
                var count = playerInventory[i].Count;
                if (setItems.ContainsKey(i))
                {
                    Assert.AreEqual(setItems[i].ID,id);
                    Assert.AreEqual(setItems[i].Count,count);
                    continue;
                }
                Assert.AreEqual(ItemConstant.NullItemId,id);
                Assert.AreEqual(ItemConstant.NullItemCount,count);
            }
        }
    }
}