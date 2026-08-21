using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    ///     残り設置数の変更イベントを購読しモデルへ適用する（前例 HotbarNetworkEventHandler）
    ///     Subscribes to remaining-placement change events and applies them to the model (precedent: HotbarNetworkEventHandler)
    /// </summary>
    public class RemainingPlacementCountEventHandler : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ClientRemainingPlacementCountDatastore _datastore;

        public RemainingPlacementCountEventHandler(IVanillaApiEvent vanillaApiEvent, ClientRemainingPlacementCountDatastore datastore)
        {
            _vanillaApiEvent = vanillaApiEvent;
            _datastore = datastore;
        }

        public void Initialize()
        {
            _vanillaApiEvent.SubscribeEventResponse(RemainingPlacementCountChangedEventPacket.EventTag, OnChanged);
        }

        /// <summary>
        ///     残り設置数の変更イベント
        ///     Remaining-placement change event
        /// </summary>
        private void OnChanged(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack>(payload);
            _datastore.Apply(packet.WalletBlockId, packet.RemainingCount);
        }
    }
}
