using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    // TrainUnitのpre sim差分イベント受信ハンドラ
    // Network handler for TrainUnit pre-simulation diff events.
    public sealed class TrainUnitPreSimulationDiffEventNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _cache;
        private IDisposable _subscription;

        public TrainUnitPreSimulationDiffEventNetworkHandler(TrainUnitFutureMessageBuffer futureMessageBuffer, TrainUnitClientCache cache)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _cache = cache;
        }

        public void Initialize()
        {
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(TrainUnitPreSimulationDiffEventPacket.EventTag, OnEventReceived);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnEventReceived(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return;
            }

            var message = MessagePackSerializer.Deserialize<TrainUnitPreSimulationDiffMessagePack>(payload);
            if (message?.Diffs == null || message.Diffs.Count == 0)
            {
                return;
            }

            _futureMessageBuffer.EnqueuePre(message.ServerTick, message.TickSequenceId, CreateBufferedEvent(message));

            #region Internal

            ITrainTickBufferedEvent CreateBufferedEvent(TrainUnitPreSimulationDiffMessagePack messagePack)
            {
                return TrainTickBufferedEvent.Create(TrainUnitPreSimulationDiffEventPacket.EventTag, ApplyDiffs);

                void ApplyDiffs()
                {
                    for (var i = 0; i < messagePack.Diffs.Count; i++)
                    {
                        _cache.ApplyPreSimulationDiff(messagePack.Diffs[i]);
                    }
                }
            }

            #endregion
        }
    }
}
