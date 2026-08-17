using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.Hotbar
{
    /// <summary>
    ///     ホットバー更新イベントを購読し ClientHotbarDatastore へ適用する（前例 NetworkEventInventoryUpdater）
    ///     Subscribes to the hotbar update event and applies it to ClientHotbarDatastore (precedent: NetworkEventInventoryUpdater)
    /// </summary>
    public class HotbarNetworkEventHandler : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ClientHotbarDatastore _clientHotbarDatastore;

        public HotbarNetworkEventHandler(IVanillaApiEvent vanillaApiEvent, ClientHotbarDatastore clientHotbarDatastore)
        {
            _vanillaApiEvent = vanillaApiEvent;
            _clientHotbarDatastore = clientHotbarDatastore;
        }

        public void Initialize()
        {
            _vanillaApiEvent.SubscribeEventResponse(HotbarUpdateEventPacket.EventTag, OnHotbarUpdateEvent);
        }

        /// <summary>
        ///     ホットバー割当の更新イベント
        ///     Hotbar assignment update event
        /// </summary>
        private void OnHotbarUpdateEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<HotbarUpdateEventPacket.HotbarUpdateEventMessagePack>(payload);
            _clientHotbarDatastore.ApplyAssignments(packet.Assignments);
        }
    }
}
