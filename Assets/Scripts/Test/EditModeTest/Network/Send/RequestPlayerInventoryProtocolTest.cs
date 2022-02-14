using System.Linq;
using MainGame.Network;
using MainGame.Network.Send;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;

namespace Test.EditModeTest.Network.Send
{
    public class RequestPlayerInventoryProtocolTest
    {
        [Test]
        public void SendTest()
        {
            var socket = new TestSocketModule();
            int playerId = 10;

            var protocol = new RequestPlayerInventoryProtocol(socket,new PlayerConnectionSetting(playerId));

            protocol.Send();
             
            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(3,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(playerId,  bytes.MoveNextToGetInt()); 
            
            
        }
    }
}