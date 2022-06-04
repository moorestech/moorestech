using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Util;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using Server;
using Server.Event;
using Server.Protocol.PacketResponse;
using Server.StartServerSystem;
using Server.Util;
using Test.Module.TestConfig;
using Test.Module.TestMod;
using World.Event;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class PlayerMainInventoryUpdateTest
    {
        private const int PlayerId = 0;
        [Test]
        public void UpdateTest()
        {

            throw new NotImplementedException();var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(0, response.Count);

            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 3));
            payload.AddRange(ToByteList.Convert(0));
            packetResponse.GetPacketResponse(payload);

            //インベントリにアイテムを追加
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            playerInventoryData.MainOpenableInventory.SetItem(5, serviceProvider.GetService<ItemStackFactory>().Create(1, 5));
            
            //追加時のイベントのキャッチ
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(1, response.Count);
            //チェック
            var byteData = new ByteListEnumerator(response[0].ToList());
            byteData.MoveNextToGetShort();
            Assert.AreEqual(1, byteData.MoveNextToGetShort());
            Assert.AreEqual(5, byteData.MoveNextToGetInt());
            Assert.AreEqual(1, byteData.MoveNextToGetInt());
            Assert.AreEqual(5, byteData.MoveNextToGetInt());
            
            
            
            

            //インベントリ内のアイテムの移動を実際に移動のプロトコルを用いてテストする
            //分割のイベントのテスト
            packetResponse.GetPacketResponse(PlayerInventoryItemMove(true,5,  3));
            packetResponse.GetPacketResponse(PlayerInventoryItemMove(false,4, 3));
            
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            
            Assert.AreEqual(4, response.Count);
            var grabUp = new ByteListEnumerator(response[0].ToList());
            var setMainInventory = new ByteListEnumerator(response[1].ToList());
            var outMainInventory = new ByteListEnumerator(response[2].ToList());
            var grabDwon = new ByteListEnumerator(response[3].ToList());
            
            grabUp.MoveNextToGetShort();//イベントパケットを示すID
            setMainInventory.MoveNextToGetShort();
            outMainInventory.MoveNextToGetShort();
            grabDwon.MoveNextToGetShort();

            Assert.AreEqual(5, grabUp.MoveNextToGetShort()); //イベントIDの確認 アイテムを持ち上げる
            Assert.AreEqual(1, setMainInventory.MoveNextToGetShort()); //インベントリのアイテムがへる
            Assert.AreEqual(1, outMainInventory.MoveNextToGetShort()); //インベントリにアイテムがセットされる
            Assert.AreEqual(5, grabDwon.MoveNextToGetShort());//アイテムが置かれる

            Assert.AreEqual(0,grabUp.MoveNextToGetInt()); //移動時のスロット確認
            Assert.AreEqual(5,setMainInventory.MoveNextToGetInt());
            Assert.AreEqual(4, outMainInventory.MoveNextToGetInt());
            Assert.AreEqual(0, grabDwon.MoveNextToGetInt());

            Assert.AreEqual(1, grabUp.MoveNextToGetInt()); //アイテムIDの確認
            Assert.AreEqual(1, setMainInventory.MoveNextToGetInt());
            Assert.AreEqual(1, outMainInventory.MoveNextToGetInt());
            Assert.AreEqual(0, grabDwon.MoveNextToGetInt());

            Assert.AreEqual(3,grabUp.MoveNextToGetInt()); //アイテム数の確認
            Assert.AreEqual(2,setMainInventory.MoveNextToGetInt());
            Assert.AreEqual(3, outMainInventory.MoveNextToGetInt());
            Assert.AreEqual(0, grabDwon.MoveNextToGetInt());

            
            
            
            

            //合成のテスト
            packetResponse.GetPacketResponse(PlayerInventoryItemMove(true,4,  3));
            packetResponse.GetPacketResponse(PlayerInventoryItemMove(false,5, 3));
            
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            
            Assert.AreEqual(4, response.Count);
            grabUp = new ByteListEnumerator(response[0].ToList());
            setMainInventory = new ByteListEnumerator(response[1].ToList());
            outMainInventory = new ByteListEnumerator(response[2].ToList());
            grabDwon = new ByteListEnumerator(response[3].ToList());
            
            grabUp.MoveNextToGetShort();
            setMainInventory.MoveNextToGetShort();
            outMainInventory.MoveNextToGetShort();
            grabDwon.MoveNextToGetShort();
            
            Assert.AreEqual(5, grabUp.MoveNextToGetShort()); //イベントIDの確認 アイテムを持ち上げる
            Assert.AreEqual(1, setMainInventory.MoveNextToGetShort()); //インベントリのアイテムがへる
            Assert.AreEqual(1, outMainInventory.MoveNextToGetShort()); //インベントリにアイテムがセットされる
            Assert.AreEqual(5, grabDwon.MoveNextToGetShort());//アイテムが置かれる

            Assert.AreEqual(0,grabUp.MoveNextToGetInt()); //移動時のスロット確認
            Assert.AreEqual(4,setMainInventory.MoveNextToGetInt());
            Assert.AreEqual(5, outMainInventory.MoveNextToGetInt());
            Assert.AreEqual(0, grabDwon.MoveNextToGetInt());

            Assert.AreEqual(1,grabUp.MoveNextToGetInt());//アイテムIDの確認
            Assert.AreEqual(0,setMainInventory.MoveNextToGetInt()); 
            Assert.AreEqual(1, outMainInventory.MoveNextToGetInt());
            Assert.AreEqual(0, grabDwon.MoveNextToGetInt());

            Assert.AreEqual(3,grabUp.MoveNextToGetInt()); //アイテム数の確認
            Assert.AreEqual(0,setMainInventory.MoveNextToGetInt());
            Assert.AreEqual(5, outMainInventory.MoveNextToGetInt());
            Assert.AreEqual(0, grabDwon.MoveNextToGetInt());
        }


        List<byte> EventRequestData(int plyaerID)
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(plyaerID)).ToList();;
        }
        private List<byte> PlayerInventoryItemMove(bool toGrab,int inventorySlot,int itemCount)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 5));
            payload.Add(toGrab ? (byte) 0 : (byte) 1);
            payload.Add(0);
            payload.AddRange(ToByteList.Convert(PlayerId));
            payload.AddRange(ToByteList.Convert(inventorySlot));
            payload.AddRange(ToByteList.Convert(itemCount));
            payload.AddRange(ToByteList.Convert(0));
            payload.AddRange(ToByteList.Convert(0));

            return payload;
        }
    }
}