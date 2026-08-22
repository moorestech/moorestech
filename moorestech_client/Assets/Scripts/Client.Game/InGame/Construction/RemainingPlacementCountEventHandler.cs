using Client.Network.API;
using Core.Master;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    ///     残り設置数変更を購読しモデルへ適用
    ///     Subscribes to remaining-placement changes and applies them to the model
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
            _vanillaApiEvent.SubscribeEventResponse(RemainingPlacementCountChangedEventPacket.EventTag, OnRemainingPlacementCountEventReceived);
        }

        /// <summary>
        ///     残り設置数の変更イベントを受信しモデルへ反映する
        ///     Receives the remaining-placement change event and applies it to the model
        /// </summary>
        private void OnRemainingPlacementCountEventReceived(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack>(payload);
            _datastore.Apply(new BlockId(packet.WalletBlockId), packet.RemainingCount);
        }
    }
}
