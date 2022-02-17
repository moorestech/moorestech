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
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();

            var response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(0, response.Count);

            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 3));
            payload.AddRange(ToByteList.Convert(0));
            packetResponse.GetPacketResponse(payload);

            //インベントリにアイテムを追加
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetMainInventoryData(0);


            playerInventoryData.SetItem(5, serviceProvider.GetService<ItemStackFactory>().Create(1, 5));
            //イベントのキャッチ
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(1, response.Count);
            //チェック
            var byteData = new ByteArrayEnumerator(response[0].ToList());
            byteData.MoveNextToGetShort();
            Assert.AreEqual(1, byteData.MoveNextToGetShort());
            Assert.AreEqual(5, byteData.MoveNextToGetInt());
            Assert.AreEqual(1, byteData.MoveNextToGetInt());
            Assert.AreEqual(5, byteData.MoveNextToGetInt());

            //インベントリ内のアイテムの移動を実際に移動のプロトコルを用いてテストする
            //分割のテスト
            packetResponse.GetPacketResponse(PlayerInventoryItemMove(5, 4, 3, 0));
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(2, response.Count);
            var byteData1 = new ByteArrayEnumerator(response[0].ToList());
            var byteData2 = new ByteArrayEnumerator(response[1].ToList());
            byteData1.MoveNextToGetShort();
            byteData2.MoveNextToGetShort();

            Assert.AreEqual(1, byteData1.MoveNextToGetShort());
            Assert.AreEqual(1, byteData2.MoveNextToGetShort()); //イベントIDの確認

            var slots = new List<int>() {byteData1.MoveNextToGetInt(), byteData2.MoveNextToGetInt()};
            Assert.True(slots.Contains(4));
            Assert.True(slots.Contains(5)); //移動時のスロット確認

            Assert.AreEqual(1, byteData1.MoveNextToGetInt());
            Assert.AreEqual(1, byteData2.MoveNextToGetInt()); //アイテムIDの確認

            var counts = new List<int>() {byteData1.MoveNextToGetInt(), byteData2.MoveNextToGetInt()};
            Assert.True(counts.Contains(2));
            Assert.True(counts.Contains(3)); //アイテム数の確認


            //合成のテスト
            packetResponse.GetPacketResponse(PlayerInventoryItemMove(4, 5, 3, 0));
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(2, response.Count);
            byteData1 = new ByteArrayEnumerator(response[0].ToList());
            byteData2 = new ByteArrayEnumerator(response[1].ToList());
            byteData1.MoveNextToGetShort();
            byteData2.MoveNextToGetShort();
            Assert.AreEqual(1, byteData1.MoveNextToGetShort());
            Assert.AreEqual(1, byteData2.MoveNextToGetShort()); //イベントIDの確認

            slots = new List<int>() {byteData1.MoveNextToGetInt(), byteData2.MoveNextToGetInt()};
            Assert.True(slots.Contains(4));
            Assert.True(slots.Contains(5)); //移動時のスロット確認

            var ids = new List<int>() {byteData1.MoveNextToGetInt(), byteData2.MoveNextToGetInt()};
            Assert.True(ids.Contains(1));
            Assert.True(ids.Contains(0)); //アイテムIDの確認

            counts = new List<int>() {byteData1.MoveNextToGetInt(), byteData2.MoveNextToGetInt()};
            Assert.True(counts.Contains(5));
            Assert.True(counts.Contains(0)); //アイテム数の確認
        }


        List<byte> EventRequestData(int plyaerID)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 4));
            payload.AddRange(ToByteList.Convert(plyaerID));
            return payload;
        }


        private List<byte> PlayerInventoryItemMove(int fromSlot, int toSlot, int itemCount, int playerId)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 6));
            payload.AddRange(ToByteList.Convert(playerId));
            payload.AddRange(ToByteList.Convert(fromSlot));
            payload.AddRange(ToByteList.Convert(toSlot));
            payload.AddRange(ToByteList.Convert(itemCount));
            return payload;
        }
    }
}