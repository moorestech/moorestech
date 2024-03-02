using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    ///     ブロックを消したらその情報がイベントで飛んでくるテスト
    /// </summary>
    public class BlockRemoveEventPacketTest
    {
        [Test]
        public void RemoveBlockEvent()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            //イベントキューにIDを登録する
            var response = packetResponse.GetPacketResponse(EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(0, eventMessagePack.Events.Count);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();


            //ブロックを設置
            BlockPlace(4, 0, 1, worldBlock, blockFactory);
            BlockPlace(3, 1, 2, worldBlock, blockFactory);
            BlockPlace(2, 3, 3, worldBlock, blockFactory);
            BlockPlace(1, 4, 4, worldBlock, blockFactory);

            //イベントを取得
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(4, eventMessagePack.Events.Count);

            var worldDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            //一個ブロックを削除
            worldDataStore.RemoveBlock(4, 0);

            //イベントを取得
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            
            Assert.AreEqual(1, eventMessagePack.Events.Count);
            var (x, y) = AnalysisResponsePacket(eventMessagePack.Events[0].Payload);
            Assert.AreEqual(4, x);
            Assert.AreEqual(0, y);

            //二個ブロックを削除
            worldDataStore.RemoveBlock(3, 1);
            worldDataStore.RemoveBlock(1, 4);
            //イベントを取得
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(2, eventMessagePack.Events.Count);
            (x, y) = AnalysisResponsePacket(eventMessagePack.Events[0].Payload);
            Assert.AreEqual(3, x);
            Assert.AreEqual(1, y);
            (x, y) = AnalysisResponsePacket(eventMessagePack.Events[1].Payload);
            Assert.AreEqual(1, x);
            Assert.AreEqual(4, y);
        }

        private void BlockPlace(int x, int y, int id, IWorldBlockDatastore worldBlock, IBlockFactory blockFactory)
        {
            worldBlock.AddBlock(blockFactory.Create(id, new Random().Next()), x, y, BlockDirection.North);
        }

        private List<byte> EventRequestData(int plyaerID)
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(plyaerID)).ToList();
        }

        private (int, int) AnalysisResponsePacket(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockEventMessagePack>(payload.ToArray());

            return (data.X, data.Y);
        }
    }
}