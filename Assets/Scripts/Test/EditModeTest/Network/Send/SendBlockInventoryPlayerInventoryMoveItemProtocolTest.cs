using System.Linq;
using MainGame.Network.Send;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;
using UnityEngine;

namespace Test.EditModeTest.Network.Send
{
    /// <summary>
    /// プレイヤーインベントリとブロックインベントリのアイテムの移動のプロトコルのテスト
    /// </summary>
    public class SendBlockInventoryPlayerInventoryMoveItemProtocolTest
    {
        [Test]
        public void SendTest()
        {
            var socket = new TestSocketModule();
            var protocol = new SendBlockInventoryPlayerInventoryMoveItemProtocol(socket);
            bool toBlock = true;
            int playerId = 10;
            int playerInventorySlot = 3;
            Vector2Int blockPos = new Vector2Int(123, -112);
            int blockInventorySlot = 5;
            int itemCount = 23;
            
            
            protocol.Send(toBlock, playerId, playerInventorySlot, blockPos, blockInventorySlot, itemCount);
            
            
            
            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(5,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(toBlock,  bytes.MoveNextToGetShort() == 0); 
            Assert.AreEqual(playerId,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(playerInventorySlot,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(blockPos.x,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(blockPos.y,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(blockInventorySlot,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(itemCount,  bytes.MoveNextToGetInt()); 
        }
    }
}