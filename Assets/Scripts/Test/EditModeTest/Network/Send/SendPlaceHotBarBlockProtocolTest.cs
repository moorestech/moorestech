using System.Linq;
using MainGame.Basic;
using MainGame.Network;
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
            int playerId = 100;
            var socket = new TestSocketModule();
            var placeHotBarBlockProtocol = new SendPlaceHotBarBlockProtocol(socket,new PlayerConnectionSetting(playerId));
            
            //送信する
            int x = 3;
            int y = 10;
            short hotBarSlot = 2;
            placeHotBarBlockProtocol.Send(x,y,hotBarSlot,BlockDirection.South);
            
            //データの検証
            var bytes = new ByteArrayEnumerator(socket.SentData.ToList());
            Assert.AreEqual(8,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(hotBarSlot,  bytes.MoveNextToGetShort()); 
            Assert.AreEqual(x,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(y,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(playerId,  bytes.MoveNextToGetInt()); 
            Assert.AreEqual(2,  bytes.MoveNextToGetByte()); 
        }
    }
}