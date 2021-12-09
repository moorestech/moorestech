using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Util;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using Server;
using Server.Event;
using Server.Util;
using World.Event;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class PlayerInventoryUpdateTest
    {
        [Test]
        public void UpdateTest()
        {
            var (packetResponse,serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();

            var response =  packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(0,response.Count);
            
            var payload = new List<byte>();
            payload.AddRange(ByteListConverter.ToByteArray((short)3));
            payload.AddRange(ByteListConverter.ToByteArray(0));
            packetResponse.GetPacketResponse(payload);
            
            //インベントリにアイテムを追加
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            
            
            playerInventoryData.SetItem(5,serviceProvider.GetService<ItemStackFactory>().Create(1,5));
            //イベントのキャッチ
            response =  packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(1,response.Count);
            //チェック
            var byteData = new ByteArrayEnumerator(response[0].ToList());
            byteData.MoveNextToGetShort();
            Assert.AreEqual(1,byteData.MoveNextToGetShort());
            Assert.AreEqual(5,byteData.MoveNextToGetInt());
            Assert.AreEqual(1,byteData.MoveNextToGetInt());
            Assert.AreEqual(5,byteData.MoveNextToGetInt());

            //TODO インベントリ内のアイテムの移動当たりを実際に移動のプロトコルを用いてテストする
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