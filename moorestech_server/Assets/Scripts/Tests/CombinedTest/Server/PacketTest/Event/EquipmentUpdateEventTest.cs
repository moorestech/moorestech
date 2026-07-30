using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.EquipmentProtocol;
using static Server.Protocol.PacketResponse.PlayerInventoryResponseProtocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class EquipmentUpdateEventTest
    {
        private const int PlayerId = 0;
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 装備変更と選択変更がイベントで飛ぶ()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var equipmentInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;

            // 装備書込みはスロット専用イベント
            // Equipment writes use the slot event
            equipmentInventory.SetItem(1, ToolItemId(), 1);

            var slotEvents = TakeSlotEvents(sink);
            Assert.AreEqual(1, slotEvents.Count);
            Assert.AreEqual(1, slotEvents[0].Slot);
            Assert.AreEqual(ToolItemId(), slotEvents[0].Item.Id);
            Assert.AreEqual(1, slotEvents[0].Item.Count);

            // 選択変更はサーバーと専用イベントへ反映
            // Selection updates server state and its dedicated event
            var request = MessagePackSerializer.Serialize(EquipmentProtocolMessagePack.CreateSetSelectedIndexRequest(PlayerId, 2));
            packet.GetPacketResponse(request, new PacketResponseContext(null));

            Assert.AreEqual(2, equipmentInventory.SelectedEquipmentIndex);
            var selectedEvents = TakeSelectedIndexEvents(sink);
            Assert.AreEqual(1, selectedEvents.Count);
            Assert.AreEqual(2, selectedEvents[0].SelectedIndex);

            #region Internal

            List<EquipmentSlotUpdateEventMessagePack> TakeSlotEvents(CapturedEventSink eventSink)
            {
                return eventSink.TakeAll()
                    .Where(capturedEvent => capturedEvent.Tag == EquipmentSlotUpdateEventPacket.EventTag)
                    .Select(capturedEvent => MessagePackSerializer.Deserialize<EquipmentSlotUpdateEventMessagePack>(capturedEvent.Payload))
                    .ToList();
            }

            List<EquipmentSelectedIndexUpdateEventMessagePack> TakeSelectedIndexEvents(CapturedEventSink eventSink)
            {
                return eventSink.TakeAll()
                    .Where(capturedEvent => capturedEvent.Tag == EquipmentSelectedIndexUpdateEventPacket.EventTag)
                    .Select(capturedEvent => MessagePackSerializer.Deserialize<EquipmentSelectedIndexUpdateEventMessagePack>(capturedEvent.Payload))
                    .ToList();
            }

            #endregion
        }

        [Test]
        public void プレイヤーインベントリ応答に装備と選択インデックスが同梱される()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var equipmentInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;
            equipmentInventory.SetItem(0, ToolItemId(), 1);
            equipmentInventory.SetSelectedEquipmentIndex(1);

            // 装備の初期データは専用プロトコルを持たずインベントリ応答へ同梱される
            // Equipment has no dedicated fetch protocol; its initial data rides on the inventory response
            var payload = MessagePackSerializer.Serialize(new RequestPlayerInventoryProtocolMessagePack(PlayerId));
            var response = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(packet.GetPacketResponse(payload, new PacketResponseContext(null))[0]);

            Assert.AreEqual(MasterHolder.ToolMaster.EquipmentSlotCount, response.Equipment.Length);
            Assert.AreEqual(ToolItemId(), response.Equipment[0].Id);
            Assert.AreEqual(1, response.Equipment[0].Count);
            Assert.AreEqual(ItemMaster.EmptyItemId, response.Equipment[1].Id);
            Assert.AreEqual(0, response.Equipment[1].Count);
            Assert.AreEqual(1, response.SelectedEquipmentIndex);
        }

        private ItemId ToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
        }
    }
}
