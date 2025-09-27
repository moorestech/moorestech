using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.InvokeBlockStateEventProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class InvokeBlockStateEventProtocolTest
    {
        [Test]
        public void InvokeTest()
        {
            // Arrange
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlock = ServerContext.WorldBlockDatastore;
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            
            // ブロックを設置
            var blockPosition = new Vector3Int(10, 20, 30);
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, blockPosition, BlockDirection.North, out var block);
            
            // イベントキューをクリア
            var playerId = 0;
            eventProtocolProvider.GetEventBytesList(playerId);
            
            // Act
            var request = new RequestInvokeBlockStateProtocolMessagePack(blockPosition);
            var payload = MessagePackSerializer.Serialize(request).ToList();
            var response = packet.GetPacketResponse(payload);
            
            // Assert
            // プロトコルがnullを返すため、レスポンスが空になることを確認
            Assert.IsNotNull(response);
            Assert.AreEqual(0, response.Count);
            
            // イベントが発行されたことを確認
            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(1, events.Count);
            var eventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(block.BlockPositionInfo);
            Assert.AreEqual(eventTag, events[0].Tag);
            
            // イベントペイロードの確認
            var blockStateMessage = MessagePackSerializer.Deserialize<BlockStateMessagePack>(events[0].Payload);
            Assert.AreEqual(blockPosition, (Vector3Int)blockStateMessage.Position);
        }
        
        [Test]
        public void NotInvokeTest()
        {
            // Arrange
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            
            var blockPosition = new Vector3Int(100, 200, 300); // 存在しない座標
            var playerId = 0;
            eventProtocolProvider.GetEventBytesList(playerId);
            
            // Act
            var request = new RequestInvokeBlockStateProtocolMessagePack(blockPosition);
            var payload = MessagePackSerializer.Serialize(request).ToList();
            var response = packet.GetPacketResponse(payload);
            
            // Assert
            // プロトコルがnullを返すため、レスポンスが空になることを確認
            Assert.IsNotNull(response);
            Assert.AreEqual(0, response.Count);
            
            // イベントが発行されていないことを確認
            var events = eventProtocolProvider.GetEventBytesList(playerId);
            Assert.AreEqual(0, events.Count);
        }
    }
}