using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltTransport;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltNetworkEventHandler
    {
        public BeltClientReplica Replica { get; }
        public BeltNetworkEventHandler(InitialHandshakeResponse initial, IVanillaApiEvent events)
        {
            Replica = new BeltClientReplica(initial.BeltSnapshot.ToCore());
            // 生成時に購読し、finalizerのバッファ再生より先に受信口を備える。
            // Subscribe during construction before the finalizer replays buffered events.
            events.SubscribeEventResponse(BeltTickCompletedEventPacket.EventTag, Receive);
            #region Internal
            void Receive(byte[] payload)
            {
                var difference = MessagePackSerializer.Deserialize<BeltTickMessagePack>(payload).ToCore();
                Replica.Receive(difference);
            }
            #endregion
        }
    }
}
