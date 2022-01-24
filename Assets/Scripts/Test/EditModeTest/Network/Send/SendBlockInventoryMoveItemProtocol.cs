using System.Linq;
using MainGame.Network.Interface.Send;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;
using UnityEngine;

namespace Test.EditModeTest.Network.Send
{
    /// <summary>
    /// ブロックインベントリないのアイテムを移動するプロトコルのテスト
    /// </summary>
    public class SendBlockInventoryMoveItemProtocol
    {
        [Test]
        public void SendTest()
        {
            var socket = new TestSocketModule();
            ISendBlockInventoryMoveItemProtocol protocol = null;
            var pos = new Vector2Int(100, 210);
            var fromSlot = 1;
            var toSlot = 3;
            var itemCount = 12;
            
            protocol.Send(pos,fromSlot,toSlot,itemCount);
            
            
            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(7,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(pos.x,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(pos.y,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(fromSlot,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(toSlot,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(itemCount,  bytes.MoveNextToGetInt()); 
        }
    }
}