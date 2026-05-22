using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    // va:event:ridingState を購読し、自分のサーバー起因降車を反映する（仕様書セクション5.2・9）。
    // Subscribes to va:event:ridingState and applies this client's server-initiated dismounts.
    public sealed class RidingStateEventHandler : IInitializable, IDisposable
    {
        private readonly TrainCarRidingState _trainCarRidingState;
        private readonly TrainCarRidingPlayerController _ridingPlayerController;
        private IDisposable _subscription;

        public RidingStateEventHandler(TrainCarRidingState trainCarRidingState, TrainCarRidingPlayerController ridingPlayerController)
        {
            _trainCarRidingState = trainCarRidingState;
            _ridingPlayerController = ridingPlayerController;
        }

        public void Initialize()
        {
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(RidingStateEventPacket.EventTag, OnEventReceived);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnEventReceived(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var message = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(payload);
            if (message == null) return;

            // 自分のイベントのみ処理する（仕様書セクション9: ローカルプレイヤーのみ）。
            // Only handle this client's own events (local-player-only scope).
            if (message.PlayerId != ClientContext.PlayerConnectionSetting.PlayerId) return;

            // サーバー起因の降車のみ反映する。乗車イベントは自分の RideAction レスポンスで反映済み。
            // Apply server-initiated dismounts only; ride events are already applied via the RideAction response.
            if (message.StateType == RidingStateEventType.Dismount && _trainCarRidingState.IsRiding)
            {
                _ridingPlayerController.ApplyDismount();
            }
        }
    }
}
