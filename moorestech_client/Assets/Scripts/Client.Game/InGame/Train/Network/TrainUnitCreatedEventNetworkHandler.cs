using System;
using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    public sealed class TrainUnitCreatedEventNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private IDisposable _subscription;

        public TrainUnitCreatedEventNetworkHandler(TrainUnitFutureMessageBuffer futureMessageBuffer)
        {
            _futureMessageBuffer = futureMessageBuffer;
        }

        public void Initialize()
        {
            // 新規列車生成イベントを購読する
            // Subscribe to new train unit events
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(TrainUnitCreatedEventPacket.EventTag, OnEventReceived);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        #region Internal

        private void OnEventReceived(byte[] payload)
        {
            // 受信イベントを適用する
            // Apply the incoming event
            if (payload == null || payload.Length == 0) return;

            var message = MessagePackSerializer.Deserialize<TrainUnitCreatedEventMessagePack>(payload);
            if (message?.Snapshot == null) return;

            // スナップショットを将来tickバッファへ投入する
            // Push snapshot into the future-tick buffer
            var bundle = message.Snapshot.ToModel();
            _futureMessageBuffer.EnqueueCreated(bundle, message.ServerTick);
        }

        #endregion
    }
}
