using Client.Game.InGame.Context;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public class NetworkEventInventoryUpdater : IInitializable
    {
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;
        
        public NetworkEventInventoryUpdater(LocalPlayerInventoryController localPlayerInventoryController)
        {
            _localPlayerInventoryController = localPlayerInventoryController;
        }
        
        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(GrabInventoryUpdateEventPacket.EventTag, OnGrabInventoryUpdateEvent);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MainInventoryUpdateEventPacket.EventTag, OnMainInventoryUpdateEvent);
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
    }
}