using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;
using System;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class BlockPlaceEventPacketTest
    {
        //ブロックを設置しなかった時何も返ってこないテスト
        [Test]
        public void DontBlockPlaceTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 0);
            
            Assert.AreEqual(0, sink.TakeAll().Count);
        }
        
        //ブロックを0個以上設置した時にブロック設置イベントが返ってくるテスト
        [Test]
        public void BlockPlaceEvent()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 0);
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;

            //初期ロード後にLoadで購読する設計のため、テストでは明示的にLoadを呼ぶ
            //Placement broadcasts subscribe in post-load Load by design, so invoke Load explicitly in the test
            serviceProvider.GetService<PlaceBlockEventPacket>().Load();

            //イベントキューにIDを登録する
            Assert.AreEqual(0, sink.TakeAll().Count);
            
            var random = new Random(1410);
            
            //ランダムな位置にブロックを設置する
            var blocks = new List<TestBlockData>();
            for (var j = 0; j < 10; j++)
            {
                var x = random.Next(-10000, 10000);
                var y = random.Next(-10000, 10000);
                var pos = new Vector3Int(x, y);
                var blockId = random.Next(1, 20);
                var direction = random.Next(0, 4);
                
                //設置したブロックを保持する
                blocks.Add(new TestBlockData(pos, (BlockId)blockId, direction));
                //ブロックの設置
                worldBlockDataStore.TryAddBlock((BlockId)blockId, pos, (BlockDirection)direction, Array.Empty<BlockCreateParam>(), out _);
            }
            
            
            //イベントパケットをリクエストする
            var events = sink.TakeAll();
            
            //返ってきたイベントパケットと設置したブロックを照合し、あったら削除する
            foreach (var r in events)
            {
                var b = AnalysisResponsePacket(r.Payload);
                for (var j = 0; j < blocks.Count; j++)
                    if (b.Equals(blocks[j]))
                        blocks.RemoveAt(j);
            }
            
            //設置したブロックリストが残ってなければすべてのイベントが返ってきた事がわかる
            Assert.AreEqual(0, blocks.Count);
            
            
            //イベントのリクエストを送ったので次は何も返ってこないテスト
            Assert.AreEqual(0, sink.TakeAll().Count);
        }
        
        //初期ロード中の設置はクライアントへ配信されず、ロード後の設置のみ配信されることを検証する
        //Verify load-time placements are not broadcast and only post-load placements are delivered to clients
        [Test]
        public void 初期ロードの設置は配信されずロード後の設置のみ配信される()
        {
            //ブロック1個を含むセーブデータを用意する
            //Prepare save data containing one placed block
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var saveJson = saveServiceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            //別ワールドを生成し、PlaceBlockEventPacketを購読させる前にロードする（ServerInstanceManagerと同じ順序）
            //Create a fresh world and load BEFORE subscribing PlaceBlockEventPacket (same order as ServerInstanceManager)
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var loadSink = EventTestUtil.RegisterCaptureSink(loadServiceProvider, 0);
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            //本番経路でロード後の購読を始める
            //Start post-load subscriptions through the same bulk initialization path as production
            foreach (var postLoadInitializable in loadServiceProvider.GetServices<IPostLoadInitializable>()) postLoadInitializable.Load();

            //ロード分の設置イベントは配信されていないこと
            //Load-time placement events must not have been broadcast
            var afterLoadEvents = loadSink.TakeAll();
            Assert.AreEqual(0, afterLoadEvents.Count(e => e.Tag == PlaceBlockEventPacket.EventTag));

            //ロード後の通常設置は配信されること
            //A normal placement after load must be broadcast
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(10, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var afterPlaceEvents = loadSink.TakeAll();
            Assert.AreEqual(1, afterPlaceEvents.Count(e => e.Tag == PlaceBlockEventPacket.EventTag));
        }

        private TestBlockData AnalysisResponsePacket(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<PlaceBlockEventMessagePack>(payload).BlockData;
            
            return new TestBlockData(data.BlockPos, data.BlockId, data.Direction);
        }
        
        private class TestBlockData
        {
            public readonly BlockDirection BlockDirection;
            public readonly BlockId id;
            public readonly int X;
            public readonly int Y;
            
            public TestBlockData(Vector3Int pos, BlockId id, int blockDirectionNum)
            {
                X = pos.x;
                Y = pos.y;
                this.id = id;
                BlockDirection = (BlockDirection)blockDirectionNum;
            }
            
            public override bool Equals(object? obj)
            {
                var b = obj as TestBlockData;
                return
                    b.id == id &&
                    b.X == X &&
                    b.Y == Y &&
                    b.BlockDirection == BlockDirection;
            }
        }
    }
}
