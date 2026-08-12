using System;
using System.Linq;
using Core.Master;
using Game.Block.Interface.Extension;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// Assign/Clear/SwapとGetHotbar、イベントパケットの3点セットを検証
    /// Verifies the Assign/Clear/Swap operations, GetHotbar, and the event packet.
    /// </summary>
    public class HotbarProtocolTest
    {
        private const int PlayerId = 1;

        [Test]
        public void Assign_Clear_Swapが反映されGetHotbarで読める()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // カタログで解決できる実在ブロックGuidを割当対象に使う（坂はカタログ対象外なので除く）
            // Use a real, catalog-resolvable block GUID as the assignment target (slopes are excluded from the catalog)
            var validId = MasterHolder.BlockMaster.Blocks.Data.First(b => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(b.BlockGuid)).BlockGuid;

            // Assign(slot3, 実在ID) → GetHotbar応答[3]が一致
            // Assign then read back via GetHotbar
            SendHotbar(HotbarProtocol.HotbarProtocolMessagePack.CreateAssignRequest(PlayerId, 3, validId));
            var afterAssign = GetHotbar();
            Assert.AreEqual(validId, afterAssign.Assignments[3]);
            Assert.AreEqual(Guid.Empty, afterAssign.Assignments[5]);

            // Swap(3, 5) → [5]に移動
            // Swap moves the assignment from slot 3 to slot 5
            SendHotbar(HotbarProtocol.HotbarProtocolMessagePack.CreateSwapRequest(PlayerId, 3, 5));
            var afterSwap = GetHotbar();
            Assert.AreEqual(Guid.Empty, afterSwap.Assignments[3]);
            Assert.AreEqual(validId, afterSwap.Assignments[5]);

            // Clear(5) → Guid.Empty
            // Clear resets the slot to Guid.Empty
            SendHotbar(HotbarProtocol.HotbarProtocolMessagePack.CreateClearRequest(PlayerId, 5));
            var afterClear = GetHotbar();
            Assert.AreEqual(Guid.Empty, afterClear.Assignments[5]);

            #region Internal

            void SendHotbar(HotbarProtocol.HotbarProtocolMessagePack request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                packet.GetPacketResponse(payload, new PacketResponseContext(null));
            }

            GetHotbarProtocol.ResponseGetHotbarMessagePack GetHotbar()
            {
                var request = new GetHotbarProtocol.RequestGetHotbarMessagePack(PlayerId);
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponse(payload, new PacketResponseContext(null));
                return MessagePackSerializer.Deserialize<GetHotbarProtocol.ResponseGetHotbarMessagePack>(responses[0]);
            }

            #endregion
        }

        [Test]
        public void 割当変更でイベントパケットが積まれる()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            var validId = MasterHolder.BlockMaster.Blocks.Data.First(b => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(b.BlockGuid)).BlockGuid;

            // Assign後、EventProtocolProviderに"va:event:hotbarUpdate"が積まれ全量9個が入っている
            // Assign enqueues a hotbar update event carrying all 9 slots
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateAssignRequest(PlayerId, 2, validId);
            var payload = MessagePackSerializer.Serialize(request);
            packet.GetPacketResponse(payload, new PacketResponseContext(null));

            var events = sink.TakeAll().Where(e => e.Tag == HotbarUpdateEventPacket.EventTag).ToList();
            Assert.AreEqual(1, events.Count);

            var data = MessagePackSerializer.Deserialize<HotbarUpdateEventPacket.HotbarUpdateEventMessagePack>(events[0].Payload);
            Assert.AreEqual(9, data.Assignments.Length);
            Assert.AreEqual(validId, data.Assignments[2]);
        }
    }
}
