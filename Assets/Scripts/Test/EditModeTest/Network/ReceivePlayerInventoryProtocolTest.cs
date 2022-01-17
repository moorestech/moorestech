using System.Collections.Generic;
using MainGame.Constant;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Receive;
using MainGame.Network.Util;
using Maingame.Types;
using NUnit.Framework;
using Test.TestModule;
using UnityEditor;

namespace Test.EditModeTest.Network
{
    public class ReceivePlayerInventoryProtocolTest
    {
        //ReceivePlayerInventoryProtocol単体のテスト
        [Test]
        public void ReceivedPacketToPlayerInventoryTest()
        {
            var inventoryEvent = new PlayerInventoryUpdateEvent();
            var protocol = new ReceivePlayerInventoryProtocol(inventoryEvent);
            var inventoryDataStore = new TestPlayerInventoryDataStore(inventoryEvent);

            var playerId = 10;
            
            var setItems = new Dictionary<int,ItemStack>();
            //セットするアイテムを定義
            setItems.Add(0,new ItemStack(1,10));
            setItems.Add(1,new ItemStack(2,5));
            setItems.Add(24,new ItemStack(3,23));
            setItems.Add(PlayerInventoryConstant.MainInventorySize-1,new ItemStack(100,19));
            
            
            
            //パケットを解析
            protocol.Analysis(CreatePlayerInventoryPacket(playerId,setItems));
            
            
            
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


        //ReceivePlayerInventoryProtocol単体のテスト
        [Test]
        public void MultiplePlayerInventoryTest()
        {
            int seed = 123151;
            var random = new System.Random(seed);
            var playerInventories = new Dictionary<int, Dictionary<int,ItemStack>>();
            
            //インベントリの作成
            for (int i = 0; i < 10; i++)
            {
                var playerId = random.Next(0,100);
                var setItems = new Dictionary<int,ItemStack>();
                for (int j = 0; j < PlayerInventoryConstant.MainInventorySize; j++)
                {
                    //1/2の確率でアイテムを設定
                    if (random.Next(0,2) == 0)
                    {
                        setItems.Add(j,new ItemStack(random.Next(1,100),random.Next(1,100)));
                    }
                }
                playerInventories.Add(playerId,setItems);
            }
            
            
            
            var inventoryEvent = new PlayerInventoryUpdateEvent();
            var protocol = new ReceivePlayerInventoryProtocol(inventoryEvent);
            var inventoryDataStore = new TestPlayerInventoryDataStore(inventoryEvent);
            //データを送信
            
            foreach (var playerInventory in playerInventories)
            {
                var playerId = playerInventory.Key;
                var setItems = playerInventory.Value;
                //パケットを解析
                protocol.Analysis(CreatePlayerInventoryPacket(playerId,setItems));
            }
            
            
            //データを検証
            foreach (var p in playerInventories)
            {
                var playerId = p.Key;
                var setItems = p.Value;
                Assert.True(inventoryDataStore.playerInventory.ContainsKey(playerId));
                Assert.AreEqual(10,inventoryDataStore.playerInventory.Count);
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
        
        
        //AllReceivePacketAnalysisService経由のテスト
        [Test]
        public void ReceivedPacketToPlayerInventoryViaAllReceivePacketAnalysisServiceTestTest()
        {
            var inventoryEvent = new PlayerInventoryUpdateEvent();
            var protocol = new AllReceivePacketAnalysisService(new ChunkUpdateEvent(),inventoryEvent);
            var inventoryDataStore = new TestPlayerInventoryDataStore(inventoryEvent);

            var playerId = 10;
            
            var setItems = new Dictionary<int,ItemStack>();
            //セットするアイテムを定義
            setItems.Add(0,new ItemStack(1,10));
            setItems.Add(1,new ItemStack(2,5));
            setItems.Add(24,new ItemStack(3,23));
            setItems.Add(PlayerInventoryConstant.MainInventorySize-1,new ItemStack(100,19));
            
            
            
            //パケットを解析
            protocol.Analysis(CreatePlayerInventoryPacket(playerId,setItems).ToArray());
            
            
            
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

        //アイテムからプレイヤーインベントリのパケットを作る
        private List<byte> CreatePlayerInventoryPacket(int playerId,Dictionary<int, ItemStack> items)
        {
            var packet = new List<byte>();
            packet.AddRange(ToByteList.Convert((short)4));
            packet.AddRange(ToByteList.Convert(playerId));
            packet.AddRange(ToByteList.Convert((short)0));

            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                if (items.ContainsKey(i))
                {
                    packet.AddRange(ToByteList.Convert(items[i].ID));   
                    packet.AddRange(ToByteList.Convert(items[i].Count));   
                    continue;
                }
                packet.AddRange(ToByteList.Convert(ItemConstant.NullItemId));   
                packet.AddRange(ToByteList.Convert(ItemConstant.NullItemCount));   
            }

            return packet;
        }
    }
}