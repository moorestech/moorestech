// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
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