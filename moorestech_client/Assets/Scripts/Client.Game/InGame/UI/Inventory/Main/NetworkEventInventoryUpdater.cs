using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Equipment;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public class NetworkEventInventoryUpdater : IInitializable
    {
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;
        private readonly LocalPlayerEquipment _localPlayerEquipment;

        public NetworkEventInventoryUpdater(LocalPlayerInventoryController localPlayerInventoryController, LocalPlayerEquipment localPlayerEquipment)
        {
            _localPlayerInventoryController = localPlayerInventoryController;
            _localPlayerEquipment = localPlayerEquipment;
        }

        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(GrabInventoryUpdateEventPacket.EventTag, OnGrabInventoryUpdateEvent);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MainInventoryUpdateEventPacket.EventTag, OnMainInventoryUpdateEvent);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(EquipmentUpdateEventPacket.EventTag, ApplyEquipmentUpdateEvent);
        }

        /// <summary>
        ///     Grabインベントリの更新イベント
        /// </summary>
        private void OnGrabInventoryUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(payload);
            var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _localPlayerInventoryController.SetGrabItem(item);
        }

        /// <summary>
        ///     メインインベントリの更新イベント
        /// </summary>
        private void OnMainInventoryUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(payload);
            var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _localPlayerInventoryController.SetMainItem(packet.Slot, item);
        }

        /// <summary>
        ///     装備の更新イベント。EventTypeごとに意味のあるフィールドだけを読む。
        ///     購読ハンドラだが、テストがwireペイロードを直接流し込めるようpublicにしている。
        ///     Equipment update event; each EventType reads only the fields that are meaningful for it.
        ///     It is the subscription handler, exposed as public so tests can feed wire payloads straight in.
        /// </summary>
        public void ApplyEquipmentUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<EquipmentUpdateEventMessagePack>(payload);
            switch (packet.EventType)
            {
                // スロット更新のSelectedIndexは無意味なので読まない
                // A slot update carries no meaningful SelectedIndex, so it is never read here
                case EquipmentUpdateEventMessagePack.SlotEventType:
                    var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
                    _localPlayerEquipment.ApplySlotUpdate(packet.Slot, item);
                    break;
                // 選択変更のSlotは番兵、Itemは空アイテムなのでどちらも読まない
                // A selected update carries a sentinel Slot and an empty Item, so neither is read here
                case EquipmentUpdateEventMessagePack.SelectedEventType:
                    _localPlayerEquipment.ApplySelected(packet.SelectedIndex);
                    break;
                default:
                    Debug.LogError("unknown equipment update event type  eventType:" + packet.EventType);
                    break;
            }
        }
    }
}
