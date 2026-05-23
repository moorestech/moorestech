using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    // va:event:ridingState を購読し、自分のサーバー起因降車を TrainCarRidingState に反映する（仕様書セクション5.2・9）。
    // 実際の親解除と pose 復帰は TrainHUDScreenState → PlayerStateController → RidingPlayerState.OnExit が行う。
    // Subscribes to va:event:ridingState and applies this client's server-initiated dismounts to TrainCarRidingState.
    // Actual unparenting and dismount pose are handled by TrainHUDScreenState → PlayerStateController → RidingPlayerState.OnExit.
    public sealed class RidingStateEventHandler : IInitializable, IDisposable
    {
        private readonly TrainCarRidingState _trainCarRidingState;
        private IDisposable _subscription;

        public RidingStateEventHandler(TrainCarRidingState trainCarRidingState)
        {
            _trainCarRidingState = trainCarRidingState;
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

            // 自分のイベントのみ処理する。
            // Process only this client's events.
            if (message.PlayerId != ClientContext.PlayerConnectionSetting.PlayerId) return;

            // サーバー起因の降車のみ反映する。乗車イベントは自分の RideAction レスポンスで反映済み。
            // Apply server-initiated dismounts only; ride events are already applied via the RideAction response.
            if (message.StateType == RidingStateEventType.Dismount && _trainCarRidingState.IsRiding)
            {
                _trainCarRidingState.ClearRidingTrainCar();
            }
        }
    }
}
