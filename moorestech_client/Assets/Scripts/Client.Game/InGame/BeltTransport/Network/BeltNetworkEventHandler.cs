using Client.Network.API;
using Client.Game.InGame.Train.Network;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltTransport;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltNetworkEventHandler
    {
        public BeltClientReplica Replica { get; }
        public BeltNetworkEventHandler(InitialHandshakeResponse initial, IVanillaApiEvent events, TrainUnitFutureMessageBuffer futureMessages)
        {
            Replica = new BeltClientReplica(initial.BeltSnapshot.ToCore());
            // 生成時に購読し、finalizerのバッファ再生より先に受信口を備える。
            // Subscribe during construction before the finalizer replays buffered events.
            events.SubscribeEventResponse(BeltTickCompletedEventPacket.EventTag, Receive);
            #region Internal
            void Receive(byte[] payload)
            {
                var message = MessagePackSerializer.Deserialize<BeltTickMessagePack>(payload);
                // 受信では進めず、送信順の共通seqでtick全体を再現する。
                // Replay the entire tick through the shared send-order sequence, not on receipt.
                futureMessages.EnqueueEvent(message.ServerTick, message.TickSequenceId,
                    TrainTickBufferedEvent.Create(() => Replica.Receive(message.ToCore())));
            }
            #endregion
        }
    }
}
