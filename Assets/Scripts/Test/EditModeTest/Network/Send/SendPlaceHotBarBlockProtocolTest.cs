using System.Linq;
using System.Net.Sockets;
using MainGame.Network.Interface.Send;
using MainGame.Network.Send;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;

namespace Test.EditModeTest.Network.Send
{
    public class SendPlaceHotBarBlockProtocolTest
    {
        [Test]
        public void SendTest()
        {
            var socket = new TestSocketModule();
            ISendPlaceHotBarBlockProtocol placeHotBarBlockProtocol = new SendPlaceHotBarBlockProtocol(socket);
            
            //送信する
            int x = 3;
            int y = 10;
            short hotBarSlot = 2;
            int playerId = 100;
            placeHotBarBlockProtocol.Send(x,y,hotBarSlot,playerId);
            
            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(8,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(hotBarSlot,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(x,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(y,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(playerId,  bytes.MoveNextToGetInt()); 
        }
    }
}