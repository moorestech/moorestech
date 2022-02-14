using System.Linq;
using MainGame.Network;
using MainGame.Network.Send;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;

namespace Test.EditModeTest.Network.Send
{
    /// <summary>
    /// イベントリクエストのプロトコルのテスト
    /// </summary>
    public class RequestEventProtocolTest
    {
        [Test]
        public void SendTest()
        {
            var socket = new TestSocketModule();
            var playerId = 1;
            var protocol = new RequestEventProtocol(socket,new PlayerConnectionSetting(playerId));
            
            protocol.Send(playerId);
            
            
            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(4,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(playerId,  bytes.MoveNextToGetInt()); 
        }
    }
}