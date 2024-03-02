using Client.Game.Context;
using Client.Network.API;
using Core.Item;
using MainGame.UnityView.UI.Inventory.Sub;
using MessagePack;
using Server.Event.EventReceive;
using ServerServiceProvider;
using VContainer.Unity;

namespace MainGame.UnityView.UI.Inventory.Main
{
    public class NetworkEventInventoryUpdater : IInitializable
    {
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly BlockInventoryView _blockInventoryView;
        
        public NetworkEventInventoryUpdater(LocalPlayerInventoryController localPlayerInventoryController, MoorestechServerServiceProvider moorestechServerServiceProvider, BlockInventoryView blockInventoryView)
        {
            _localPlayerInventoryController = localPlayerInventoryController;
            _itemStackFactory = moorestechServerServiceProvider.ItemStackFactory;
            _blockInventoryView = blockInventoryView;
        }
        
        public void Initialize()
        {
            MoorestechContext.VanillaApi.Event.RegisterEventResponse(GrabInventoryUpdateEventPacket.EventTag,OnGrabInventoryUpdateEvent);
            MoorestechContext.VanillaApi.Event.RegisterEventResponse(MainInventoryUpdateEventPacket.EventTag,OnMainInventoryUpdateEvent);
            MoorestechContext.VanillaApi.Event.RegisterEventResponse(OpenableBlockInventoryUpdateEventPacket.EventTag,OnOpenableBlockInventoryUpdateEvent);
        }
        
        /// <summary>
        /// Grabインベントリの更新イベント
        /// </summary>
        private void OnGrabInventoryUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(payload);
            var item = _itemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _localPlayerInventoryController.SetGrabItem(item);
        }

        /// <summary>
        /// メインインベントリの更新イベント
        /// </summary>
        private void OnMainInventoryUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(payload);
            var item = _itemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _localPlayerInventoryController.SetMainItem(packet.Slot,item);
        }
        
        /// <summary>
        /// 開いているブロックのインベントリの更新イベント
        /// </summary>
        private void OnOpenableBlockInventoryUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(payload);
            var item = _itemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _blockInventoryView.SetItemSlot(packet.Slot,item);
        }
    }
}