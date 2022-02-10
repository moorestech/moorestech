using System.Linq;
using MainGame.Network.Send;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;

namespace Test.EditModeTest.Network.Send
{
    /// <summary>
    /// プレイヤーのインベントリ内でアイテムを移動させた時のテスト
    /// </summary>
    public class PlayerInventoryMoveItemProtocolTest
    {
        [Test]
        public void SendTest()
        {
            var socket = new TestSocketModule();
            var protocol = new SendPlayerInventoryMoveItemProtocol(socket);
            var playerId = 1;
            var fromSlot = 10;
            var toSlot = 20;
            var itemCount = 55;
            
            protocol.Send(playerId, fromSlot, toSlot, itemCount);
            
            
            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(6,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(playerId,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(fromSlot,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(toSlot,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(itemCount,  bytes.MoveNextToGetInt()); 
        }
    }
}