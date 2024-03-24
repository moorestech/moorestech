using Client.Game.Context;
using Client.Game.UI.Inventory.Sub;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.UI.Inventory.Main
{
    public class NetworkEventInventoryUpdater : IInitializable
    {
        private readonly BlockInventoryView _blockInventoryView;
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;

        public NetworkEventInventoryUpdater(LocalPlayerInventoryController localPlayerInventoryController, BlockInventoryView blockInventoryView)
        {
            _localPlayerInventoryController = localPlayerInventoryController;
            _blockInventoryView = blockInventoryView;
        }

        public void Initialize()
        {
            MoorestechContext.VanillaApi.Event.RegisterEventResponse(GrabInventoryUpdateEventPacket.EventTag, OnGrabInventoryUpdateEvent);
            MoorestechContext.VanillaApi.Event.RegisterEventResponse(MainInventoryUpdateEventPacket.EventTag, OnMainInventoryUpdateEvent);
            MoorestechContext.VanillaApi.Event.RegisterEventResponse(OpenableBlockInventoryUpdateEventPacket.EventTag, OnOpenableBlockInventoryUpdateEvent);
        }

        /// <summary>
        ///     Grabインベントリの更新イベント
        /// </summary>
        private void OnGrabInventoryUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(payload);
            var item = MoorestechContext.ServerServices.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _localPlayerInventoryController.SetGrabItem(item);
        }

        /// <summary>
        ///     メインインベントリの更新イベント
        /// </summary>
        private void OnMainInventoryUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(payload);
            var item = MoorestechContext.ServerServices.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _localPlayerInventoryController.SetMainItem(packet.Slot, item);
        }

        /// <summary>
        ///     開いているブロックのインベントリの更新イベント
        /// </summary>
        private void OnOpenableBlockInventoryUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(payload);
            var item = MoorestechContext.ServerServices.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _blockInventoryView.SetItemSlot(packet.Slot, item);
        }
    }
}