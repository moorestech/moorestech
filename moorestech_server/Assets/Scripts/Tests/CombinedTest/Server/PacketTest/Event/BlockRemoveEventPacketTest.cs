using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using System;
using Server.Protocol;

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

            //初期ロード後にLoadで購読する設計のため、テストでは明示的にLoadを呼ぶ
            //Placement broadcasts subscribe in post-load Load by design, so invoke Load explicitly in the test
            serviceProvider.GetService<PlaceBlockEventPacket>().Load();

            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 0);
            //捕捉sinkを登録し初期分を空にする
            //Register the capture sink and drain initial events
            Assert.AreEqual(0, sink.TakeAll().Count);
            var worldBlock = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            //ブロックを設置
            BlockPlace(4, 0, 1, worldBlock, blockFactory);
            BlockPlace(3, 1, 2, worldBlock, blockFactory);
            BlockPlace(2, 3, 3, worldBlock, blockFactory);
            BlockPlace(1, 4, 4, worldBlock, blockFactory);
            
            //イベントを取得
            //Take the captured events
            var events = sink.TakeAll();
            Assert.AreEqual(4, events.Count);
            
            var worldDataStore = ServerContext.WorldBlockDatastore;
            //一個ブロックを削除
            //Remove one block
            worldDataStore.RemoveBlock(new Vector3Int(4, 0), BlockRemoveReason.ManualRemove);
            
            //イベントを取得
            //Take the captured events
            events = sink.TakeAll();
            
            Assert.AreEqual(1, events.Count);
            var pos = AnalysisResponsePacket(events[0].Payload);
            Assert.AreEqual(4, pos.x);
            Assert.AreEqual(0, pos.y);
            
            //二個ブロックを削除
            //Remove two more blocks
            worldDataStore.RemoveBlock(new Vector3Int(3, 1), BlockRemoveReason.ManualRemove);
            worldDataStore.RemoveBlock(new Vector3Int(1, 4), BlockRemoveReason.ManualRemove);
            //イベントを取得
            //Take the captured events
            events = sink.TakeAll();
            Assert.AreEqual(2, events.Count);
            pos = AnalysisResponsePacket(events[0].Payload);
            Assert.AreEqual(3, pos.x);
            Assert.AreEqual(1, pos.y);
            pos = AnalysisResponsePacket(events[1].Payload);
            Assert.AreEqual(1, pos.x);
            Assert.AreEqual(4, pos.y);
        }
        
        private void BlockPlace(int x, int y, int id, IWorldBlockDatastore worldBlockDatastore, IBlockFactory blockFactory)
        {
            worldBlockDatastore.TryAddBlock((BlockId)id, new Vector3Int(x, y), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }
        
        private Vector3Int AnalysisResponsePacket(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockEventMessagePack>(payload);
            
            return data.Position;
        }
    }
}
