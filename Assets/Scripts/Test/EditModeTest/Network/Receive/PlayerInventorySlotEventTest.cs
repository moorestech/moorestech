using System.Collections.Generic;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Util;
using Maingame.Types;
using NUnit.Framework;
using Test.TestModule;

namespace Test.EditModeTest.Network.Receive
{
    public class PlayerInventorySlotEventTest
    {
        [Test]
        public void UpdatePlayerInventorySlotToDataStoreTest()
        {
            var playerInventoryEvent = new PlayerInventoryUpdateEvent();
            var inventoryDatastore = new TestPlayerInventoryDataStore(playerInventoryEvent);
            var analysis = new AllReceivePacketAnalysisService(new ChunkUpdateEvent(),playerInventoryEvent);
            
            
            //他のテストコードのインベントリパケット作成モジュールを使用
            var playerId = 10;
            var packet =
                new ReceivePlayerInventoryProtocolTest().
                    CreatePlayerInventoryPacket(
                        playerId,
                    new Dictionary<int, ItemStack>());
            
            //プロトコル経由でプレイヤーインベントリの作成
            analysis.Analysis(packet.ToArray());
            
            
            var items = inventoryDatastore.playerInventory[playerId];
            //スロットの更新を行う
            analysis.Analysis(CreateSlotInventory(5,new ItemStack(10,3)).ToArray());
            //更新したスロットの確認
            Assert.AreEqual(new ItemStack(10,3),items[5]);
            
            analysis.Analysis(CreateSlotInventory(5,new ItemStack(1,3)).ToArray());
            Assert.AreEqual(new ItemStack(1,3),items[5]);
            
            analysis.Analysis(CreateSlotInventory(17,new ItemStack(2,10)).ToArray());
            Assert.AreEqual(new ItemStack(1,3),items[5]);
            Assert.AreEqual(new ItemStack(2,10),items[17]);
            
        }

        private List<byte> CreateSlotInventory(int slot,ItemStack itemStack)
        {
            var packet = new List<byte>();
            packet.AddRange(ToByteList.Convert((short)3));
            packet.AddRange(ToByteList.Convert((short)1));
            packet.AddRange(ToByteList.Convert(slot));
            packet.AddRange(ToByteList.Convert(itemStack.ID));
            packet.AddRange(ToByteList.Convert(itemStack.Count));
            return packet;
        }
    }
}