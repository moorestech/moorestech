using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using Server.Event;
using Server.Event.EventReceive.EventRegister;
using Server.Util;
using World.Event;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class PlayerInventoryUpdateTest
    {
        [Test]
        public void UpdateTest()
        {
            var (packetResponse,serviceProvider) = PacketResponseCreatorGenerators.Create();
            new RegisterSendClientEvents(serviceProvider.GetService<BlockPlaceEvent>(),serviceProvider.GetService<EventProtocolProvider>());

            var response =  packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(0,response.Count);
            
            var payload = new List<byte>();
            payload.AddRange(ByteListConverter.ToByteArray((short)3));
            payload.AddRange(ByteListConverter.ToByteArray(0));
            packetResponse.GetPacketResponse(payload);
            
            //インベントリにアイテムを追加
            var playerInventoryData = serviceProvider.GetService<PlayerInventoryDataStore>().GetInventoryData(0);
            
            
            playerInventoryData.SetItem(5,ItemStackFactory.Create(1,5));
            //イベントのキャッチ
            response =  packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(1,response.Count);
            //チェック
            var byteData = new ByteArrayEnumerator(response[0].ToList());
            byteData.MoveNextToGetShort();
            Assert.AreEqual(2,byteData.MoveNextToGetShort());
            Assert.AreEqual(5,byteData.MoveNextToGetInt());
            Assert.AreEqual(1,byteData.MoveNextToGetInt());
            Assert.AreEqual(5,byteData.MoveNextToGetInt());

            //アイテムをドロップしたときのテスト
            playerInventoryData.DropItem(5,2);
            response =  packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(1,response.Count);
            byteData = new ByteArrayEnumerator(response[0].ToList());
            byteData.MoveNextToGetShort();
            byteData.MoveNextToGetShort();
            Assert.AreEqual(5,byteData.MoveNextToGetInt());
            Assert.AreEqual(1,byteData.MoveNextToGetInt());
            Assert.AreEqual(3,byteData.MoveNextToGetInt());
            
            //アイテムをドロップしたときのテスト
            playerInventoryData.DropItem(5,3);
            response =  packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(1,response.Count);
            byteData = new ByteArrayEnumerator(response[0].ToList());
            byteData.MoveNextToGetShort();
            byteData.MoveNextToGetShort();
            Assert.AreEqual(5,byteData.MoveNextToGetInt());
            Assert.AreEqual(ItemConst.NullItemId,byteData.MoveNextToGetInt());
            Assert.AreEqual(0,byteData.MoveNextToGetInt());
        }
        
        
        List<byte> EventRequestData(int plyaerID)
        {
            var payload = new List<byte>();
            payload.AddRange(ByteListConverter.ToByteArray((short)4));
            payload.AddRange(ByteListConverter.ToByteArray(plyaerID));
            return payload;
        }
    }
}