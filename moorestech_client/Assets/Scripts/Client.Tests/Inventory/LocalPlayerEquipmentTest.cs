using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Network.API;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UniRx;
using static Server.Protocol.PacketResponse.EquipmentProtocol;
using static Server.Protocol.PacketResponse.PlayerInventoryResponseProtocol;

namespace Client.Tests.Inventory
{
    /// <summary>
    ///     サーバー権威の装備がクライアントの装備モデルへ届くかを、実プロトコル/実イベントのペイロードで検証する
    ///     Verifies that server-authoritative equipment reaches the client model, using real protocol and event payloads
    /// </summary>
    public class LocalPlayerEquipmentTest
    {
        private const int PlayerId = 0;

        [Test]
        public void 装備と素手の選択インデックスがインベントリ応答から復元される()
        {
            var (packet, serviceProvider) = CreateServer();
            var equipmentInventory = GetEquipmentInventory(serviceProvider);
            equipmentInventory.SetItem(0, ToolItemId(), 1);
            equipmentInventory.SetSelectedEquipmentIndex(LocalPlayerEquipment.BareHandsIndex);

            // 応答messagepack→クライアントDTOの変換で装備が落ちないことを確かめる
            // Ensures equipment survives the response messagepack to client DTO conversion
            var clientResponse = new PlayerInventoryResponse(RequestInventoryResponse(packet));
            var equipment = new LocalPlayerEquipment();
            equipment.ApplyInitial(clientResponse.Equipment, clientResponse.SelectedEquipmentIndex);

            Assert.AreEqual(ToolItemId(), equipment.Slots[0].Id);
            Assert.AreEqual(LocalPlayerEquipment.BareHandsIndex, equipment.SelectedIndex);
            Assert.AreEqual(ItemMaster.EmptyItemId, equipment.SelectedItem.Id);
        }

        [Test]
        public void 素手への選択変更がプロトコルとイベントを往復してモデルへ届く()
        {
            var (packet, serviceProvider) = CreateServer();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var equipment = new LocalPlayerEquipment();
            var updater = CreateUpdater(equipment);
            equipment.ApplyInitial(new List<IItemStack> { ServerContext.ItemStackFactory.Create(ToolItemId(), 1) }, 0);

            // 素手(-1)は装備選択の特別値なので、送信→サーバー→イベント→適用まで通す
            // Bare hands (-1) is the special value of this design, so it is driven through send, server, event and apply
            var request = MessagePackSerializer.Serialize(EquipmentProtocolMessagePack.CreateSetSelectedIndexRequest(PlayerId, LocalPlayerEquipment.BareHandsIndex));
            packet.GetPacketResponse(request, new PacketResponseContext(null));
            updater.ApplyEquipmentUpdateEvent(TakeEquipmentPayload(sink));

            Assert.AreEqual(LocalPlayerEquipment.BareHandsIndex, GetEquipmentInventory(serviceProvider).SelectedEquipmentIndex);
            Assert.AreEqual(LocalPlayerEquipment.BareHandsIndex, equipment.SelectedIndex);
            Assert.AreEqual(ItemMaster.EmptyItemId, equipment.SelectedItem.Id);

            // 選択変更イベントのSlotは番兵なので、スロットは書き換わらない
            // The selected event's Slot is a sentinel, so no slot may be overwritten
            Assert.AreEqual(ToolItemId(), equipment.Slots[0].Id);
        }

        [Test]
        public void スロット更新イベントは選択インデックスを書き換えない()
        {
            var (_, serviceProvider) = CreateServer();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var equipment = new LocalPlayerEquipment();
            var updater = CreateUpdater(equipment);
            equipment.ApplySelected(LocalPlayerEquipment.BareHandsIndex);

            GetEquipmentInventory(serviceProvider).SetItem(1, ToolItemId(), 1);
            updater.ApplyEquipmentUpdateEvent(TakeEquipmentPayload(sink));

            // スロットイベントのSelectedIndexを読んでいると素手が0へ化ける
            // Reading the slot event's SelectedIndex would silently turn bare hands into slot 0
            Assert.AreEqual(ToolItemId(), equipment.Slots[1].Id);
            Assert.AreEqual(LocalPlayerEquipment.BareHandsIndex, equipment.SelectedIndex);
            Assert.AreEqual(ItemMaster.EmptyItemId, equipment.SelectedItem.Id);
        }

        [Test]
        public void 選択中スロットの中身が消えると手持ちも空になる()
        {
            CreateServer();
            var itemStackFactory = ServerContext.ItemStackFactory;
            var equipment = new LocalPlayerEquipment();
            equipment.ApplyInitial(new List<IItemStack> { itemStackFactory.Create(ToolItemId(), 1) }, 0);
            Assert.AreEqual(ToolItemId(), equipment.SelectedItem.Id);

            // 選択中スロットが空になってもselectedイベントは飛ばないため、都度導出でなければ追従できない
            // No selected event is dispatched when the selected slot empties, so only per-call derivation can follow it
            equipment.ApplySlotUpdate(0, itemStackFactory.CreatEmpty());
            Assert.AreEqual(ItemMaster.EmptyItemId, equipment.SelectedItem.Id);
        }

        [Test]
        public void スロット更新と選択変更のどちらもOnChangedで通知される()
        {
            CreateServer();
            var equipment = new LocalPlayerEquipment();
            var changedCount = 0;
            equipment.OnChanged.Subscribe(_ => changedCount++);

            equipment.ApplySlotUpdate(0, ServerContext.ItemStackFactory.Create(ToolItemId(), 1));
            equipment.ApplySelected(0);

            Assert.AreEqual(2, changedCount);
        }

        private (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private NetworkEventInventoryUpdater CreateUpdater(LocalPlayerEquipment equipment)
        {
            return new NetworkEventInventoryUpdater(new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment), equipment);
        }

        private IEquipmentInventory GetEquipmentInventory(ServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;
        }

        private PlayerInventoryResponseProtocolMessagePack RequestInventoryResponse(PacketResponseCreator packet)
        {
            var payload = MessagePackSerializer.Serialize(new RequestPlayerInventoryProtocolMessagePack(PlayerId));
            return MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(packet.GetPacketResponse(payload, new PacketResponseContext(null))[0]);
        }

        private ItemId ToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(MasterHolder.ToolMaster.All[0].ToolItemGuid);
        }

        private byte[] TakeEquipmentPayload(CapturedEventSink sink)
        {
            var equipmentEvents = sink.TakeAll().Where(capturedEvent => capturedEvent.Tag == EquipmentUpdateEventPacket.EventTag).ToList();
            Assert.AreEqual(1, equipmentEvents.Count);
            return equipmentEvents[0].Payload;
        }
    }
}
