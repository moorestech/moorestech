using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.EventProtocol;
using System;

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
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            //イベントキューにIDを登録する
            List<byte[]> response = packetResponse.GetPacketResponse(EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            Assert.AreEqual(0, eventMessagePack.Events.Count);
            var worldBlock = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            //ブロックを設置
            BlockPlace(4, 0, 1, worldBlock, blockFactory);
            BlockPlace(3, 1, 2, worldBlock, blockFactory);
            BlockPlace(2, 3, 3, worldBlock, blockFactory);
            BlockPlace(1, 4, 4, worldBlock, blockFactory);
            
            //イベントを取得
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            Assert.AreEqual(4, eventMessagePack.Events.Count);
            
            var worldDataStore = ServerContext.WorldBlockDatastore;
            //一個ブロックを削除
            worldDataStore.RemoveBlock(new Vector3Int(4, 0), BlockRemoveReason.ManualRemove);
            
            //イベントを取得
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            
            Assert.AreEqual(1, eventMessagePack.Events.Count);
            var pos = AnalysisResponsePacket(eventMessagePack.Events[0].Payload);
            Assert.AreEqual(4, pos.x);
            Assert.AreEqual(0, pos.y);
            
            //二個ブロックを削除
            worldDataStore.RemoveBlock(new Vector3Int(3, 1), BlockRemoveReason.ManualRemove);
            worldDataStore.RemoveBlock(new Vector3Int(1, 4), BlockRemoveReason.ManualRemove);
            //イベントを取得
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            Assert.AreEqual(2, eventMessagePack.Events.Count);
            pos = AnalysisResponsePacket(eventMessagePack.Events[0].Payload);
            Assert.AreEqual(3, pos.x);
            Assert.AreEqual(1, pos.y);
            pos = AnalysisResponsePacket(eventMessagePack.Events[1].Payload);
            Assert.AreEqual(1, pos.x);
            Assert.AreEqual(4, pos.y);
        }
        
        private void BlockPlace(int x, int y, int id, IWorldBlockDatastore worldBlockDatastore, IBlockFactory blockFactory)
        {
            worldBlockDatastore.TryAddBlock((BlockId)id, new Vector3Int(x, y), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }
        
        private byte[] EventRequestData(int playerID)
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(playerID));
        }
        
        private Vector3Int AnalysisResponsePacket(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockEventMessagePack>(payload);
            
            return data.Position;
        }
    }
}
