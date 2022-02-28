using System.Linq;
using MainGame.Network;
using MainGame.Network.Send;
using MainGame.Network.Send.SocketUtil;
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
            var playerId = 1;
            var pos = new Vector2(123.4f, 567.8f);
            
            var protocol = new SendPlayerPositionProtocolProtocol(socket,new ReturnPlayerPosition(pos),new PlayerConnectionSetting(playerId));
            
            protocol.Send();

            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(2,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(pos.x,  bytes.MoveNextToGetFloat()); 
            Assert.AreEqual(pos.y,  bytes.MoveNextToGetFloat()); 
            Assert.AreEqual(playerId,  bytes.MoveNextToGetInt()); 
        }
    }

    class ReturnPlayerPosition : IPlayerPosition
    {
        private readonly Vector2 _pos;

        public ReturnPlayerPosition(Vector2 pos)
        {
            _pos = pos;
        }

        public Vector2 GetPlayerPosition()
        {
            return _pos;
        }
    }
        
}