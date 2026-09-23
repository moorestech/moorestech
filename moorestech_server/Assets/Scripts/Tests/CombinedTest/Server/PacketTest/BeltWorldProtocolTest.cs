using System;
using System.Collections.Generic;
using Core.Update;
using Game.BeltSegment;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.MessagePack;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack.BeltSegment;
using Tests.Module.TestMod;
using UnityEngine;
namespace Tests.CombinedTest.Server.PacketTest
{
    public sealed class BeltWorldProtocolTest
    {
        [Test]
        public void RequestsReadCompletedStateAndBroadcastFramesReplayMachineTransport()
        {
            var (packets, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = services.GetRequiredService<BeltWorldDatastore>();
            GameUpdater.RestoreCurrentTick((ulong)uint.MaxValue + 20); world.Load();
            var first = Request(); var second = Request();
            Assert.AreEqual(first.Position.Tick, second.Position.Tick); Assert.AreEqual(first.Position.Sequence, second.Position.Sequence);
            var sink = new Sink(); services.GetRequiredService<EventProtocolProvider>().RegisterPlayer(1, sink);
            services.GetRequiredService<BeltWorldEventPacket>().Load();
            Add(ForUnitTestModBlockId.ChestId, Vector3Int.back).GetComponent<VanillaChestComponent>().SetItem(0, ForUnitTestItemId.ItemId1, 8);
            Add(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero);
            Add(ForUnitTestModBlockId.ChestId, Vector3Int.forward);
            Assert.AreEqual(first.Generation, Request().Generation);
            BeltReplaySimulation replay = null; int frames = 0, replacements = 0;
            for (int tick = 0; tick < 100; tick++)
            {
                GameUpdater.Update();
                foreach (var message in sink.Events)
                {
                    if (message.Tag == BeltWorldEventPacket.SnapshotTag)
                    {
                        var snapshot = BeltWireCodec.Decode(MessagePackSerializer.Deserialize<BeltWorldSnapshotMessagePack>(message.Payload));
                        replay = new(snapshot.Simulation); replacements++;
                    }
                    if (message.Tag != BeltWorldEventPacket.FrameTag) continue;
                    var frame = BeltWireCodec.Decode(MessagePackSerializer.Deserialize<BeltWorldFrameMessagePack>(message.Payload));
                    Assert.AreEqual(replay.ComputeStateHash(), frame.PreviousHash);
                    replay.ApplyTick(frame.Replay, false); frames++;
                    Assert.AreEqual(1U, frame.Position.Sequence);
                }
                sink.Events.Clear();
                Assert.AreEqual(new BeltReplaySimulation(Request().Simulation).ComputeStateHash(), replay.ComputeStateHash());
            }
            Assert.AreEqual(100, frames); Assert.AreEqual(1, replacements);
            var oldWorld = MessagePackSerializer.Deserialize<RequestWorldDataProtocol.ResponseWorldDataMessagePack>(packets.GetPacketResponse(
                MessagePackSerializer.Serialize(new RequestWorldDataProtocol.RequestWorldDataMessagePack(1)), new PacketResponseContext(null))[0]);
            Assert.IsEmpty(oldWorld.Entities);
            #region Internal
            BeltWorldSnapshot Request()
            {
                var bytes = packets.GetPacketResponse(MessagePackSerializer.Serialize(GetBeltWorldRequest.Create()), new PacketResponseContext(null))[0];
                var envelope = MessagePackSerializer.Deserialize<GetBeltWorldResponse>(bytes);
                Assert.AreEqual(GetBeltWorldProtocol.ProtocolTag, envelope.Tag);
                return BeltWireCodec.Decode(envelope.Snapshot);
            }
            IBlock Add(global::Core.Master.BlockId id, Vector3Int position)
            {
                Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(id, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
                return block;
            }
            #endregion
        }
        private sealed class Sink : IPlayerEventSink
        {
            internal readonly List<EventMessagePack> Events = new();
            public void EnqueueEvent(EventMessagePack message) => Events.Add(message);
        }
    }
}
