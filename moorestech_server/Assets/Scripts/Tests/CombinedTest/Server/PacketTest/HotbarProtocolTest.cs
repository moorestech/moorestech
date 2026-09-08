using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Hotbar;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
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
    /// Assign/Clear/Swapと3点セットを検証
    /// Verifies the Assign/Clear/Swap operations, the resulting state, and the event packet.
    /// </summary>
    public class HotbarProtocolTest
    {
        private const int PlayerId = 1;

        [Test]
        public void Assign_Clear_Swapが割当状態へ反映される()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // カタログ解決済みの実在Guidを割当に使う
            // Use a catalog-resolvable real Guid as the assignment target
            var validId = MasterHolder.BlockMaster.Blocks.Data.First().BlockGuid;

            // 割当は解放済みブロックのみ通るため、対象を解放してから要求する
            // Assignment only accepts unlocked blocks, so unlock the target before requesting it
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlock(validId);

            // Assign後に割当状態が一致
            // Assign then read the resulting assignments back
            SendHotbar(HotbarProtocol.HotbarProtocolMessagePack.CreateAssignRequest(PlayerId, 3, validId));
            var afterAssign = ReadAssignments();
            Assert.AreEqual(validId, afterAssign[3]);
            Assert.AreEqual(Guid.Empty, afterAssign[5]);

            // Swap(3, 5) → [5]に移動
            // Swap moves the assignment from slot 3 to slot 5
            SendHotbar(HotbarProtocol.HotbarProtocolMessagePack.CreateSwapRequest(PlayerId, 3, 5));
            var afterSwap = ReadAssignments();
            Assert.AreEqual(Guid.Empty, afterSwap[3]);
            Assert.AreEqual(validId, afterSwap[5]);

            // Clear(5) → Guid.Empty
            // Clear resets the slot to Guid.Empty
            SendHotbar(HotbarProtocol.HotbarProtocolMessagePack.CreateClearRequest(PlayerId, 5));
            var afterClear = ReadAssignments();
            Assert.AreEqual(Guid.Empty, afterClear[5]);

            #region Internal

            void SendHotbar(HotbarProtocol.HotbarProtocolMessagePack request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                packet.GetPacketResponse(payload, new PacketResponseContext(null));
            }

            // 取得口はInitialHandshakeへ同梱されたため、状態はlookupから直接読む
            // The fetch endpoint moved into InitialHandshake, so the state is read straight from the lookup
            IReadOnlyList<Guid> ReadAssignments()
            {
                return serviceProvider.GetService<IHotbarAssignmentLookup>().GetAssignments(PlayerId);
            }

            #endregion
        }

        [Test]
        public void 割当変更でイベントパケットが積まれる()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            var validId = MasterHolder.BlockMaster.Blocks.Data.First().BlockGuid;

            // 割当は解放済みブロックのみ通るため、対象を解放してから要求する
            // Assignment only accepts unlocked blocks, so unlock the target before requesting it
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlock(validId);

            // Assignで全9枠がイベントに
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
