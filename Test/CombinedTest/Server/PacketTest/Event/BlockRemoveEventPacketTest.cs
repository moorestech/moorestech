using System.Collections.Generic;
using System.Linq;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Protocol;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    /// ブロックを消したらその情報がイベントで飛んでくるテスト
    /// </summary>
    public class BlockRemoveEventPacketTest
    {
        [Test]
        public void RemoveBlockEvent()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();

            //イベントキューにIDを登録する
            var response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(0, response.Count);


            //ブロックを設置
            BlockPlace(4, 0, 1, packetResponse);
            BlockPlace(3, 1, 2, packetResponse);
            BlockPlace(2, 3, 3, packetResponse);
            BlockPlace(1, 4, 4, packetResponse);

            //イベントを取得
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(4, response.Count);

            var worldDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            //一個ブロックを削除
            worldDataStore.RemoveBlock(4, 0);

            //イベントを取得
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(1, response.Count);
            var (x, y) = AnalysisResponsePacket(response[0]);
            Assert.AreEqual(4, x);
            Assert.AreEqual(0, y);

            //二個ブロックを削除
            worldDataStore.RemoveBlock(3, 1);
            worldDataStore.RemoveBlock(1, 4);
            //イベントを取得
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(2, response.Count);
            (x, y) = AnalysisResponsePacket(response[0]);
            Assert.AreEqual(3, x);
            Assert.AreEqual(1, y);
            (x, y) = AnalysisResponsePacket(response[1]);
            Assert.AreEqual(1, x);
            Assert.AreEqual(4, y);
        }

        void BlockPlace(int x, int y, int id, PacketResponseCreator packetResponseCreator)
        {
            var bytes = new List<byte>();
            bytes.AddRange(ToByteList.Convert((short) 1));
            bytes.AddRange(ToByteList.Convert(id));
            bytes.AddRange(ToByteList.Convert((short) 0));
            bytes.AddRange(ToByteList.Convert(x));
            bytes.AddRange(ToByteList.Convert(y));
            bytes.AddRange(ToByteList.Convert(0));
            bytes.AddRange(ToByteList.Convert(0));
            packetResponseCreator.GetPacketResponse(bytes);
        }

        List<byte> EventRequestData(int plyaerID)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 4));
            payload.AddRange(ToByteList.Convert(plyaerID));
            return payload;
        }

        (int, int) AnalysisResponsePacket(byte[] payload)
        {
            var b = new ByteArrayEnumerator(payload.ToList());
            b.MoveNextToGetShort();
            var eventId = b.MoveNextToGetShort();
            Assert.AreEqual(3, eventId);
            var x = b.MoveNextToGetInt();
            var y = b.MoveNextToGetInt();
            return (x, y);
        }
    }
}