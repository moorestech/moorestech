using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TickEndWorldMutationConflictTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 重なる長尺設置はFIFO先頭だけが設置と課金を行う()
        {
            // 縦3セルの電柱を1セルずらして重ね、占有競合を起こす
            // Overlap two 3-cell-tall electric poles offset by one cell to create an occupancy conflict
            var (packet, provider) = CreateServer();
            GrantRequiredItems(provider, ForUnitTestModBlockId.ElectricPoleId, 1);
            UnlockBlock(provider, ForUnitTestModBlockId.ElectricPoleId);
            var first = new PlaceInfo
            {
                Position = new Vector3Int(30, 0, 10), Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.ElectricPoleId,
            };
            var second = new PlaceInfo
            {
                Position = new Vector3Int(30, 1, 10), Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.ElectricPoleId,
            };

            // 二つを同じ固定batchへ積み、先着の占有範囲を後着に再検証させる
            // Put both in one frozen batch so the later entry rechecks the first footprint
            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            queue.Enqueue(new ProtocolEntry(packet, CreatePlacePayload(new List<PlaceInfo> { first })));
            queue.Enqueue(new ProtocolEntry(packet, CreatePlacePayload(new List<PlaceInfo> { second })));
            GameUpdater.UpdateOneTick();

            var world = ServerContext.WorldBlockDatastore;
            var placed = world.GetBlock(new Vector3Int(30, 0, 10));
            Assert.IsNotNull(placed);
            Assert.AreSame(placed, world.GetBlock(new Vector3Int(30, 2, 10)));
            AssertInventoryEmptyOfRequiredItems(provider, ForUnitTestModBlockId.ElectricPoleId);
        }

        [Test]
        public void 同じブロックの二重撤去は返却を一回だけ行う()
        {
            var (packet, provider) = CreateServer();
            var position = new Vector3Int(40, 0, 40);
            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.BlockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var payload = MessagePackSerializer.Serialize(
                new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(PlayerId, position));

            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            queue.Enqueue(new ProtocolEntry(packet, payload));
            queue.Enqueue(new ProtocolEntry(packet, payload));
            GameUpdater.UpdateOneTick();

            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(position));
            Assert.AreEqual(2, GetItemCount(GetInventory(provider), Material1Guid));
            Assert.AreEqual(1, GetItemCount(GetInventory(provider), Material2Guid));
        }

        private sealed class ProtocolEntry : ITickEndPacketEntry
        {
            private readonly PacketResponseCreator _packet;
            private readonly byte[] _payload;
            public bool IsActive => true;

            public ProtocolEntry(PacketResponseCreator packet, byte[] payload)
            {
                _packet = packet;
                _payload = payload;
            }

            public void Process()
            {
                _packet.GetPacketResponse(_payload, new PacketResponseContext(null));
            }
        }
    }
}
