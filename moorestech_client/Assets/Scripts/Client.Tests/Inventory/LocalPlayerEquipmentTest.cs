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
using static Server.Protocol.PacketResponse.SetSelectedEquipmentIndexProtocol;
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
        private static readonly System.Guid ToolItemGuid = System.Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 装備と選択インデックスがインベントリ応答から復元される()
        {
            var (packet, serviceProvider) = CreateServer();
            var equipmentInventory = GetEquipmentInventory(serviceProvider);
            equipmentInventory.SetItem(0, ToolItemId(), 1);
            equipmentInventory.SetSelectedEquipmentIndex(1);

            // 応答messagepack→クライアントDTOの変換で装備が落ちないことを確かめる
            // Ensures equipment survives the response messagepack to client DTO conversion
            var clientResponse = new PlayerInventoryResponse(RequestInventoryResponse(packet));
            var equipment = new LocalPlayerEquipment();
            equipment.Initialize(clientResponse.Equipment, clientResponse.SelectedEquipmentIndex);

            Assert.AreEqual(ToolItemId(), equipment.Slots[0].Id);
            Assert.AreEqual(1, equipment.SelectedIndex);
            Assert.AreEqual(ItemMaster.EmptyItemId, equipment.SelectedItem.Id);
        }

        [Test]
        public void 選択変更がプロトコルとイベントを往復してモデルへ届く()
        {
            var (packet, serviceProvider) = CreateServer();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var equipment = new LocalPlayerEquipment();
            var apiEvent = CreateSubscribedApiEvent(equipment);
            equipment.Initialize(new List<IItemStack> { ServerContext.ItemStackFactory.Create(ToolItemId(), 1) }, 0);

            // 空スロット(1)への切替を送信→サーバー→イベント→適用まで通す
            // Drive a switch to the empty slot (1) through send, server, event and apply
            var request = MessagePackSerializer.Serialize(new SetSelectedEquipmentIndexMessagePack(PlayerId, 1));
            packet.GetPacketResponse(request, new PacketResponseContext(null));
            apiEvent.Dispatch(EquipmentSelectedIndexUpdateEventPacket.EventTag, TakeSelectedIndexPayload(sink));

            Assert.AreEqual(1, GetEquipmentInventory(serviceProvider).SelectedEquipmentIndex);
            Assert.AreEqual(1, equipment.SelectedIndex);
            Assert.AreEqual(ItemMaster.EmptyItemId, equipment.SelectedItem.Id);

            // 選択イベントはスロットを変えない
            // Selection events do not alter slots
            Assert.AreEqual(ToolItemId(), equipment.Slots[0].Id);

            #region Internal

            byte[] TakeSelectedIndexPayload(CapturedEventSink eventSink)
            {
                var equipmentEvents = eventSink.TakeAll()
                    .Where(capturedEvent => capturedEvent.Tag == EquipmentSelectedIndexUpdateEventPacket.EventTag)
                    .ToList();
                Assert.AreEqual(1, equipmentEvents.Count);
                return equipmentEvents[0].Payload;
            }

            #endregion
        }

        [Test]
        public void スロット更新イベントは選択インデックスを書き換えない()
        {
            var (_, serviceProvider) = CreateServer();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var equipment = new LocalPlayerEquipment();
            var apiEvent = CreateSubscribedApiEvent(equipment);
            equipment.ApplySelected(2);

            GetEquipmentInventory(serviceProvider).SetItem(1, ToolItemId(), 1);
            apiEvent.Dispatch(EquipmentSlotUpdateEventPacket.EventTag, TakeSlotPayload(sink));

            // スロットイベントは選択位置を変えない
            // Slot events do not alter the selection
            Assert.AreEqual(ToolItemId(), equipment.Slots[1].Id);
            Assert.AreEqual(2, equipment.SelectedIndex);
            Assert.AreEqual(ItemMaster.EmptyItemId, equipment.SelectedItem.Id);

            #region Internal

            byte[] TakeSlotPayload(CapturedEventSink eventSink)
            {
                var equipmentEvents = eventSink.TakeAll()
                    .Where(capturedEvent => capturedEvent.Tag == EquipmentSlotUpdateEventPacket.EventTag)
                    .ToList();
                Assert.AreEqual(1, equipmentEvents.Count);
                return equipmentEvents[0].Payload;
            }

            #endregion
        }

        [Test]
        public void 選択中スロットの中身が消えると手持ちも空になる()
        {
            CreateServer();
            var itemStackFactory = ServerContext.ItemStackFactory;
            var equipment = new LocalPlayerEquipment();
            equipment.Initialize(new List<IItemStack> { itemStackFactory.Create(ToolItemId(), 1) }, 0);
            Assert.AreEqual(ToolItemId(), equipment.SelectedItem.Id);

            // 選択中スロットが空になってもselectedイベントは飛ばないため、都度導出でなければ追従できない
            // No selected event is dispatched when the selected slot empties, so only per-call derivation can follow it
            equipment.ApplySlotUpdate(0, itemStackFactory.CreatEmpty());
            Assert.AreEqual(ItemMaster.EmptyItemId, equipment.SelectedItem.Id);
        }

        [Test]
        public void スロット更新と選択変更のどちらも変更通知が飛ぶ()
        {
            CreateServer();
            var equipment = new LocalPlayerEquipment();
            var changedCount = 0;
            equipment.OnSlotsOrSelectionChanged.Subscribe(_ => changedCount++);

            Assert.AreEqual(0, equipment.SelectionConfirmationRevision);
            equipment.ApplySlotUpdate(0, ServerContext.ItemStackFactory.Create(ToolItemId(), 1));
            Assert.AreEqual(0, equipment.SelectionConfirmationRevision);
            equipment.ApplySelected(0);

            Assert.AreEqual(2, changedCount);
            Assert.AreEqual(1, equipment.SelectionConfirmationRevision);
        }

        private (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // 実イベント経路（購読登録→タグ配信）を通すため、更新器を購読済みにしたイベント口を返す
        // Returns an event port with the updater already subscribed, so payloads travel the real path (subscribe then dispatch by tag)
        private CapturingVanillaApiEvent CreateSubscribedApiEvent(LocalPlayerEquipment equipment)
        {
            var apiEvent = new CapturingVanillaApiEvent();
            new NetworkEventInventoryUpdater(apiEvent, new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment), equipment).Initialize();
            return apiEvent;
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
            return MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
        }
    }
}
