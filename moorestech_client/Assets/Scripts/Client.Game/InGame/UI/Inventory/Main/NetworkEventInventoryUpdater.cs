using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Network.API;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public class NetworkEventInventoryUpdater : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;
        private readonly LocalPlayerEquipment _localPlayerEquipment;

        public NetworkEventInventoryUpdater(IVanillaApiEvent vanillaApiEvent, LocalPlayerInventoryController localPlayerInventoryController, LocalPlayerEquipment localPlayerEquipment)
        {
            _vanillaApiEvent = vanillaApiEvent;
            _localPlayerInventoryController = localPlayerInventoryController;
            _localPlayerEquipment = localPlayerEquipment;
        }

        public void Initialize()
        {
            _vanillaApiEvent.SubscribeEventResponse(GrabInventoryUpdateEventPacket.EventTag, OnGrabInventoryUpdateEvent);
            _vanillaApiEvent.SubscribeEventResponse(MainInventoryUpdateEventPacket.EventTag, OnMainInventoryUpdateEvent);
            _vanillaApiEvent.SubscribeEventResponse(EquipmentSlotUpdateEventPacket.EventTag, OnEquipmentSlotUpdateEvent);
            _vanillaApiEvent.SubscribeEventResponse(EquipmentSelectedIndexUpdateEventPacket.EventTag, OnEquipmentSelectedIndexUpdateEvent);
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
        ///     装備スロットの更新イベント
        /// </summary>
        private void OnEquipmentSlotUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<EquipmentSlotUpdateEventMessagePack>(payload);
            var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _localPlayerEquipment.ApplySlotUpdate(packet.Slot, item);
        }

        /// <summary>
        ///     装備の選択インデックス更新イベント
        /// </summary>
        private void OnEquipmentSelectedIndexUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<EquipmentSelectedIndexUpdateEventMessagePack>(payload);
            _localPlayerEquipment.ApplySelected(packet.SelectedIndex);
        }
    }
}
