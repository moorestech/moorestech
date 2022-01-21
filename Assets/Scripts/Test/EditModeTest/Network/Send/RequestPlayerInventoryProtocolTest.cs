using System.Linq;
using MainGame.Network.Interface.Send;
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
            IRequestPlayerInventoryProtocol protocol = new RequestPlayerInventoryProtocol(socket);

            int playerId = 10;
            
            protocol.Send(playerId);
             
            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(4,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(playerId,  bytes.MoveNextToGetInt()); 
            
            
        }
    }
}