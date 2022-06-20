using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.StartServerSystem;
using Test.Module.TestMod;

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
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
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
            packetResponseCreator.GetPacketResponse(
                MessagePackSerializer.Serialize(new PutBlockProtocolMessagePack(id, x, y)).ToList());
        }

        List<byte> EventRequestData(int plyaerID)
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(plyaerID)).ToList();
        }

        (int, int) AnalysisResponsePacket(List<byte> payload)
        {

            var data = MessagePackSerializer.Deserialize<RemoveBlockEventMessagePack>(payload.ToArray());
            
            return (data.X, data.Y);
        }
    }
}