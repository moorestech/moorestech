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
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.RequestBlockStateProtocol;
using System;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RequestBlockStateProtocolTest
    {
        [Test]
        public void InvokeTest()
        {
            // Arrange
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlock = ServerContext.WorldBlockDatastore;
            
            // ブロックを設置
            var blockPosition = new Vector3Int(10, 20, 30);
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, blockPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            
            // イベントキューをクリア
            var playerId = 1;
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, playerId);
            sink.TakeAll();
            
            // Act
            var request = new RequestBlockStateProtocolMessagePack(blockPosition);
            var payload = MessagePackSerializer.Serialize(request);
            var response = packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            
            // Assert
            // プロトコルがnullを返すため、レスポンスが空になることを確認
            Assert.IsNotNull(response);
            Assert.AreEqual(0, response.Count);
            
            serviceProvider.GetService<ChangeBlockStateEventPacket>().BroadcastPendingStates();

            // イベントが発行されたことを確認
            // Ensure the bundle event was published
            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChangeBlockStateEventPacket.EventTag, events[0].Tag);

            // 束の中身の確認
            // Check the bundle content
            var states = EventTestUtil.ExtractBlockStates(events);
            Assert.AreEqual(1, states.Count);
            Assert.AreEqual(blockPosition, (Vector3Int)states[0].Position);
        }
        
        [Test]
        public void NotInvokeTest()
        {
            // Arrange
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockPosition = new Vector3Int(100, 200, 300); // 存在しない座標
            var playerId = 1;
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, playerId);
            sink.TakeAll();
            
            // Act
            var request = new RequestBlockStateProtocolMessagePack(blockPosition);
            var payload = MessagePackSerializer.Serialize(request);
            var response = packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            
            // Assert
            // プロトコルがnullを返すため、レスポンスが空になることを確認
            Assert.IsNotNull(response);
            Assert.AreEqual(0, response.Count);
            
            serviceProvider.GetService<ChangeBlockStateEventPacket>().BroadcastPendingStates();

            // イベントが発行されていないことを確認
            var events = sink.TakeAll();
            Assert.AreEqual(0, events.Count);
        }
    }
}
