using System.Linq;
using MainGame.Network.Interface.Send;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;
using UnityEngine;

namespace Test.EditModeTest.Network.Send
{
    /// <summary>
    /// プレイヤー座標の送信テスト
    /// </summary>
    public class SendPlayerPositionTest
    {
        
        [Test]
        public void SendTest()
        {
            var socket = new TestSocketModule();
            ISendPlayerPositionProtocol protocol = null;
            var playerId = 1;
            var pos = new Vector2(123.4f, 567.8f);
            
            protocol.Send(playerId, pos);
            
            
            
            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(3,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(pos.x,  bytes.MoveNextToGetFloat()); 
            Assert.AreEqual(pos.y,  bytes.MoveNextToGetFloat()); 
            Assert.AreEqual(playerId,  bytes.MoveNextToGetInt()); 
        }
    }
}