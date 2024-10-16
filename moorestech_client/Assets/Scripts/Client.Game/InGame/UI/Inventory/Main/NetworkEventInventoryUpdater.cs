using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Sub;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Inventory.Main
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
            ClientContext.VanillaApi.Event.RegisterEventResponse(GrabInventoryUpdateEventPacket.EventTag, OnGrabInventoryUpdateEvent);
            ClientContext.VanillaApi.Event.RegisterEventResponse(MainInventoryUpdateEventPacket.EventTag, OnMainInventoryUpdateEvent);
            ClientContext.VanillaApi.Event.RegisterEventResponse(OpenableBlockInventoryUpdateEventPacket.EventTag, OnOpenableBlockInventoryUpdateEvent);
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
        ///     開いているブロックのインベントリの更新イベント
        /// </summary>
        private void OnOpenableBlockInventoryUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(payload);
            var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _blockInventoryView.UpdateInventorySlot(packet.Slot, item);
        }
    }
}