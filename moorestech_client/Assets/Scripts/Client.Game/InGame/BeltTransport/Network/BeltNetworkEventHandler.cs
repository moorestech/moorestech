using Client.Network.API;
using Client.Game.InGame.Train.Network;
using Core.BeltTransport;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltTransport;
using UnityEngine;
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
                var difference = MessagePackSerializer.Deserialize<BeltTickMessagePack>(payload).ToCore();
                var order = difference.Order;
                // snapshot内の変更も空処理として順序を通し、共通seqに穴を作らない。
                // Retain snapshot-covered changes as no-ops so shared sequences stay contiguous.
                if (difference.Tick <= initial.BeltSnapshot.Tick)
                    Debug.Log($"Belt tick {difference.Tick} covered by initial snapshot {initial.BeltSnapshot.Tick}; consume its sequences without replay.");
                // 受信では進めず、trainと同じtick・seqバッファへ積む。
                // Queue onto the train tick/sequence buffer without advancing on receipt.
                for (int i = 0; i < difference.BeforeTick.Length; i++)
                    EnqueueChange(difference.BeforeTick[i], order.BeforeSequenceIds[i]);
                futureMessages.EnqueueEvent(order.ServerTick, order.SimulationSequenceId,
                    TrainTickBufferedEvent.Create(() => Replica.Advance(difference.Tick, difference.Outputs)));
                for (int i = 0; i < difference.AfterTick.Length; i++)
                    EnqueueChange(difference.AfterTick[i], order.AfterSequenceIds[i]);
                futureMessages.EnqueueEvent(order.ServerTick, order.CompletedSequenceId,
                    TrainTickBufferedEvent.Create(() => Replica.CompleteTick(difference.Tick)));

                #region Internal
                void EnqueueChange(BeltBoundaryChange change, uint sequenceId) =>
                    futureMessages.EnqueueEvent(order.ServerTick, sequenceId,
                        TrainTickBufferedEvent.Create(() => Replica.ApplyChange(difference.Tick, change)));
                #endregion
            }
            #endregion
        }
    }
}
