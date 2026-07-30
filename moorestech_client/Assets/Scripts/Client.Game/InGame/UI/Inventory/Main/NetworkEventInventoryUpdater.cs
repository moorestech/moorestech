// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Equipment;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
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
            ClientContext.VanillaApi.Event.SubscribeEventResponse(EquipmentSlotUpdateEventPacket.EventTag, OnEquipmentSlotUpdateEvent);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(EquipmentSelectedIndexUpdateEventPacket.EventTag, OnEquipmentSelectedIndexUpdateEvent);
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
        ///     購読ハンドラだが、テストがwireペイロードを直接流し込めるようpublicにしている。
        ///     It is the subscription handler, exposed as public so tests can feed wire payloads straight in.
        /// </summary>
        public void OnEquipmentSlotUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<EquipmentSlotUpdateEventMessagePack>(payload);
            var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _localPlayerEquipment.ApplySlotUpdate(packet.Slot, item);
        }

        /// <summary>
        ///     購読ハンドラだが、テストがwireペイロードを直接流し込めるようpublicにしている。
        ///     It is the subscription handler, exposed as public so tests can feed wire payloads straight in.
        /// </summary>
        public void OnEquipmentSelectedIndexUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<EquipmentSelectedIndexUpdateEventMessagePack>(payload);
            _localPlayerEquipment.ApplySelected(packet.SelectedIndex);
        }
    }
}
