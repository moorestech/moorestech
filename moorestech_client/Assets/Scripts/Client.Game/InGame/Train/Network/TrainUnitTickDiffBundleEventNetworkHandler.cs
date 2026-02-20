using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    // TickDiffBundle受信をhash+diffに分解してキューするハンドラ
    // Handler that splits TickDiffBundle into hash+diff queue entries.
    public sealed class TrainUnitTickDiffBundleEventNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _cache;
        private IDisposable _subscription;

        public TrainUnitTickDiffBundleEventNetworkHandler(TrainUnitFutureMessageBuffer futureMessageBuffer, TrainUnitClientCache cache)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _cache = cache;
        }

        public void Initialize()
        {
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(TrainUnitTickDiffBundleEventPacket.EventTag, OnEventReceived);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnEventReceived(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return;
            var message = MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(payload);
            if (message == null)
                return;
            EnqueueHash(message);
            _futureMessageBuffer.EnqueueEvent(message.ServerTick, message.DiffTickSequenceId, CreateBufferedEvent(message));
            return;

            #region Internal

            void EnqueueHash(TrainUnitTickDiffBundleMessagePack bundleMessage)
            {
                // bundleは n tick のdiffを持つのでhashは n-1 tick として展開する。
                // Bundle carries diff at tick n, so hash is expanded as tick n-1.
                if (bundleMessage.ServerTick == 0)
                    return;
                var hashTick = bundleMessage.ServerTick - 1;
                var hashMessage = new TrainUnitHashStateMessagePack(
                    bundleMessage.UnitsHash,
                    bundleMessage.RailGraphHash,
                    hashTick,
                    bundleMessage.HashTickSequenceId);
                _futureMessageBuffer.EnqueueHash(hashMessage);
            }

            ITrainTickBufferedEvent CreateBufferedEvent(TrainUnitTickDiffBundleMessagePack messagePack)
            {
                return TrainTickBufferedEvent.Create(ApplyDiffs);

                void ApplyDiffs()
                {
                    // pre diff ここではマスコンレベル差分など
                    var diffs = messagePack.Diffs;
                    if (diffs != null)
                    {
                        for (var i = 0; i < diffs.Count; i++)
                        {
                            _cache.ApplyPreSimulationDiff(diffs[i]);
                        }
                    }
                    // sim本体
                    List<ClientTrainUnit> _work = new();
                    _cache.CopyUnitsTo(_work);
                    for (var i = 0; i < _work.Count; i++)
                    {
                        _work[i].Update();
                    }
                }
            }

            #endregion
        }
    }
}
