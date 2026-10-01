using UniRx;
using System;
using System.Linq;
using Client.Game.InGame.BeltTransport;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack.BeltTransport;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltServerWireReplayTest
    {
        [Test]
        public void ActualSnapshotProtocolAndCompletedEventsReproduceServerTest()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var transport = services.GetRequiredService<BeltWorldTransport>();
            var sink = EventTestUtil.RegisterCaptureSink(services, 1);
            var source = Place(ForUnitTestModBlockId.ChestId, 0).GetComponent<IBlockInventory>();
            var first = Place(ForUnitTestModBlockId.BeltConveyorId, 1);
            Place(ForUnitTestModBlockId.BeltConveyorId, 2);
            var target = Place(ForUnitTestModBlockId.ChestId, 3).GetComponent<IBlockInventory>();
            transport.Initialize();
            var protocol = new GetBeltSnapshotProtocol(services);
            var response = (GetBeltSnapshotProtocol.Response)protocol.GetResponse(MessagePackSerializer.Serialize(GetBeltSnapshotProtocol.Request.Create()), 1);
            response = BeltWireRoundTripTest.RoundTrip(response);
            var replica = new BeltClientReplica(response.Snapshot.ToCore());
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3));
            int count = 0, notifications = 0;
            using var subscription = replica.OnStateChanged.Subscribe(_ => notifications++);
            // 実搬送をwire往復後にCPU再現。
            // Replay actual transport on the CPU after the wire round trip.
            for (int i = 0; i < 250; i++)
            {
                GameUpdater.UpdateOneTick();
                foreach (var packet in sink.TakeAll().Where(p => p.Tag == BeltTickCompletedEventPacket.EventTag))
                {
                    replica.Receive(MessagePackSerializer.Deserialize<BeltTickMessagePack>(packet.Payload).ToCore()); count++;
                }
                Assert.AreEqual(count, notifications);
                Assert.AreEqual(response.Snapshot.Tick + (ulong)count, transport.CaptureCommittedSnapshot().Tick);
                CollectionAssert.AreEqual(transport.CaptureCommittedSnapshot().Snapshot.Items, replica.Snapshot.Items);
            }
            Assert.AreEqual(250, count);
            Assert.AreEqual(3, target.GetItem(0).Count);
            var legacy = (RequestWorldDataProtocol.ResponseWorldDataMessagePack)new RequestWorldDataProtocol(services).GetResponse(MessagePackSerializer.Serialize(new RequestWorldDataProtocol.RequestWorldDataMessagePack()), 1);
            Assert.IsEmpty(legacy.Entities);

            #region Internal
            IBlock Place(Core.Master.BlockId id, int z)
            {
                Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(id, new Vector3Int(0, 0, z), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
                return block;
            }
            #endregion
        }
    }
}
