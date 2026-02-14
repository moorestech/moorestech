using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Game.Train.Unit;
using MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    // TrainCar削除イベントを受信してpost-simキューへ積む
    // Receive train-car removal events and enqueue them for post-simulation apply.
    public sealed class TrainCarRemovedEventNetworkHandler : IInitializable, IDisposable
    {
        private const string EventTag = "va:event:trainCarRemoved";

        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _cache;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private IDisposable _subscription;

        public TrainCarRemovedEventNetworkHandler(
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitClientCache cache,
            TrainCarObjectDatastore trainCarDatastore)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _cache = cache;
            _trainCarDatastore = trainCarDatastore;
        }

        public void Initialize()
        {
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(EventTag, OnEventReceived);
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

            var message = MessagePackSerializer.Deserialize<TrainCarRemovedEventMessagePack>(payload);
            if (message == null)
            {
                return;
            }

            _futureMessageBuffer.EnqueueEvent(message.ServerTick, message.TickSequenceId, CreateBufferedEvent(message));

            #region Internal

            ITrainTickBufferedEvent CreateBufferedEvent(TrainCarRemovedEventMessagePack messagePack)
            {
                return TrainTickBufferedEvent.Create(ApplyRemovedEvent);

                void ApplyRemovedEvent()
                {
                    // キャッシュと描画オブジェクトの削除を同一tickで適用する
                    // Apply cache/object removal at the same post-simulation tick.
                    var trainCarInstanceId = new TrainCarInstanceId(messagePack.TrainCarInstanceId);
                    _cache.RemoveTrainCar(trainCarInstanceId);
                    _trainCarDatastore.RemoveTrainEntity(trainCarInstanceId);
                }
            }

            #endregion
        }

        [MessagePackObject]
        internal sealed class TrainCarRemovedEventMessagePack
        {
            [Key(0)] public long TrainCarInstanceId { get; set; }
            [Key(1)] public uint ServerTick { get; set; }
            [Key(2)] public uint TickSequenceId { get; set; }

            [Obsolete("Reserved for MessagePack.")]
            public TrainCarRemovedEventMessagePack()
            {
            }

            public TrainCarRemovedEventMessagePack(long trainCarInstanceId, uint serverTick, uint tickSequenceId)
            {
                TrainCarInstanceId = trainCarInstanceId;
                ServerTick = serverTick;
                TickSequenceId = tickSequenceId;
            }
        }
    }
}
