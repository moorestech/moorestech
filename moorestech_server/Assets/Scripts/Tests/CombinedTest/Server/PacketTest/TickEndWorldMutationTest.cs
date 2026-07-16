using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
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
    public class TickEndWorldMutationTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 同じ座標への二つの設置は先着だけが設置と課金を行う()
        {
            var (packet, provider) = CreateServer();
            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            GrantRequiredItems(provider, ForUnitTestModBlockId.BlockId, 2);
            var payload = CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (4, 5));

            queue.Enqueue(new ProtocolEntry(packet, payload));
            queue.Enqueue(new ProtocolEntry(packet, payload));
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(4, 5)));
            Assert.AreEqual(2, GetItemCount(GetInventory(provider), Material1Guid));
            Assert.AreEqual(1, GetItemCount(GetInventory(provider), Material2Guid));
        }

        [Test]
        public void 一件分の素材を競合する設置は先着だけが成功する()
        {
            var (packet, provider) = CreateServer();
            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            GrantRequiredItems(provider, ForUnitTestModBlockId.BlockId, 1);

            queue.Enqueue(new ProtocolEntry(packet, CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (7, 0))));
            queue.Enqueue(new ProtocolEntry(packet, CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (8, 0))));
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(7, 0)));
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(8, 0)));
            AssertInventoryEmptyOfRequiredItems(provider, ForUnitTestModBlockId.BlockId);
        }

        [Test]
        public void 撤去と設置は全体FIFOの順で最終状態が変わる()
        {
            var (packet, provider) = CreateServer();
            var position = new Vector3Int(10, 10);
            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.BlockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            var remove = CreateRemovePayload(position);
            var place = CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (10, 10));

            queue.Enqueue(new ProtocolEntry(packet, remove));
            queue.Enqueue(new ProtocolEntry(packet, place));
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(position));
            AssertInventoryEmptyOfRequiredItems(provider, ForUnitTestModBlockId.BlockId);

            (packet, provider) = CreateServer();
            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.BlockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            queue = provider.GetRequiredService<TickEndPacketQueue>();
            queue.Enqueue(new ProtocolEntry(packet, place));
            queue.Enqueue(new ProtocolEntry(packet, remove));
            GameUpdater.UpdateOneTick();
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(position));
        }

        [Test]
        public void 過負荷予約破断は手動撤去より先で返却を発生させない()
        {
            var (packet, provider) = CreateServer();
            var position = new Vector3Int(12, 0, 12);
            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.BlockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            provider.GetRequiredService<IBlockRemovalReservationService>()
                .ReserveRemoval(block.BlockInstanceId, BlockRemoveReason.Broken);
            provider.GetRequiredService<TickEndPacketQueue>()
                .Enqueue(new ProtocolEntry(packet, CreateRemovePayload(position)));

            GameUpdater.UpdateOneTick();

            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(position));
            AssertInventoryEmptyOfRequiredItems(provider, ForUnitTestModBlockId.BlockId);
        }

        [Test]
        public void dirty電力問い合わせは次tick先頭の再構築後まで保留する()
        {
            var (packet, provider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, Vector3Int.zero,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);
            GameUpdater.UpdateOneTick();
            var electricDatastore = provider.GetRequiredService<IElectricWireNetworkLookup>();
            Assert.IsFalse(electricDatastore.IsDerivedStateDirty);

            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            queue.Enqueue(new CallbackEntry(() =>
            {
                world.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, new Vector3Int(0, 0, 2),
                    BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
                return TickEndPacketProcessResult.Completed;
            }));
            var query = MessagePackSerializer.Serialize(
                new GetElectricNetworkInfoProtocol.RequestGetElectricNetworkInfoMessagePack(pole.BlockInstanceId));
            var queryEntry = new ProtocolEntry(packet, query);
            queue.Enqueue(queryEntry);

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(electricDatastore.IsDerivedStateDirty);
            Assert.AreEqual(0, queryEntry.Responses.Count);

            GameUpdater.UpdateOneTick();
            Assert.IsFalse(electricDatastore.IsDerivedStateDirty);
            Assert.AreEqual(1, queryEntry.Responses.Count);
        }

        [Test]
        public void ConverterModeChangeDefersNetworkQueriesUntilNextTickSettlement()
        {
            var (packet, provider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;
            var position = new Vector3Int(14, 0, 14);
            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, position,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var converter);
            GameUpdater.UpdateOneTick();

            // 設定変更と同じ末尾処理内の問い合わせを、次tickの再計算後まで保留する
            // Defer queries from the same tick-end batch until the next tick recalculates both networks
            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            queue.Enqueue(new ProtocolEntry(packet, MessagePackSerializer.Serialize(
                new SetElectricToGearOutputModeRequest(position, 1))));
            var electricQuery = new ProtocolEntry(packet, MessagePackSerializer.Serialize(
                new GetElectricNetworkInfoProtocol.RequestGetElectricNetworkInfoMessagePack(converter.BlockInstanceId)));
            var gearQuery = new ProtocolEntry(packet, MessagePackSerializer.Serialize(
                new GetGearNetworkInfoProtocol.RequestGetGearNetworkInfoMessagePack(converter.BlockInstanceId)));
            queue.Enqueue(electricQuery);
            queue.Enqueue(gearQuery);

            GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, electricQuery.Responses.Count);
            Assert.AreEqual(0, gearQuery.Responses.Count);

            GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, electricQuery.Responses.Count);
            Assert.AreEqual(1, gearQuery.Responses.Count);
        }

        private static byte[] CreateRemovePayload(Vector3Int position)
        {
            return MessagePackSerializer.Serialize(
                new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(PlayerId, position));
        }

        private sealed class ProtocolEntry : ITickEndPacketEntry
        {
            private readonly PacketResponseCreator _packet;
            private readonly byte[] _payload;
            public readonly List<byte[]> Responses = new();
            public bool IsActive => true;

            public ProtocolEntry(PacketResponseCreator packet, byte[] payload)
            {
                _packet = packet;
                _payload = payload;
            }

            public TickEndPacketProcessResult Process()
            {
                var result = _packet.GetTickEndPacketResponse(_payload, new PacketResponseContext(), out var responses);
                if (result == TickEndPacketProcessResult.Completed) Responses.AddRange(responses);
                return result;
            }
        }

        private sealed class CallbackEntry : ITickEndPacketEntry
        {
            private readonly Func<TickEndPacketProcessResult> _process;
            public bool IsActive => true;
            public CallbackEntry(Func<TickEndPacketProcessResult> process) { _process = process; }
            public TickEndPacketProcessResult Process() { return _process(); }
        }
    }
}
