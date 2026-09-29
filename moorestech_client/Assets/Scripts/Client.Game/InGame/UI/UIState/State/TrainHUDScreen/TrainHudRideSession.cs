using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Train.Unit;
using Game.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Game.PlayerRiding.Interface;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.TrainHUDScreen
{
    // HUD の乗降要求と強制降車通知を管理する
    // Own ride requests and forced-dismount notifications for the HUD
    internal sealed class TrainHudRideSession
    {
        private readonly PlayerStateController _playerStateController;
        private readonly Subject<Unit> _onRidingStateChanged = new();
        private IDisposable _eventSubscription;
        private CancellationTokenSource _cts;
        private RidingPlayerStateContext _rideContext;
        private bool _dismounted;

        // 乗車中か。乗車要求の結果が返る前と降車後はどちらもfalse
        // Whether the player is riding; false both before a ride request lands and after a dismount
        internal bool IsRiding => _rideContext != null && !_dismounted;

        // 降車が確定したか。乗車要求が失敗した場合もここが立つ
        // Whether a dismount is settled; this also rises when a ride request failed
        internal bool IsDismounted => _dismounted;
        internal IObservable<Unit> OnRidingStateChanged => _onRidingStateChanged;

        internal TrainHudRideSession(PlayerStateController playerStateController)
        {
            _playerStateController = playerStateController;
        }

        internal void Enter(UITransitContext context)
        {
            // HUD表示中だけ強制降車を購読する
            // Subscribe to forced dismounts only while the HUD is active
            _eventSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RidingStateEventPacket.EventTag, OnRidingStateEventReceived);
            _rideContext = null;
            _dismounted = false;
            if (context.TryGetContext<InitialRideTrainCarRequest>(out var rideRequest))
            {
                // 既に乗車済みならサーバーへの再要求を行わない
                // Avoid another server request when the player is already riding
                var target = RidableIdentifierMessagePack.CreateTrainCarMessage(rideRequest.TargetCarId.AsPrimitive());
                _rideContext = new RidingPlayerStateContext(target, rideRequest.SeatIndex);
                _playerStateController.SetState(PlayerStateEnum.Riding, _rideContext);
                _onRidingStateChanged.OnNext(Unit.Default);
                return;
            }
            SendRideRequestAsync(context.GetContext<RideTrainCarRequest>()).Forget(LogRpcFault);
        }

        internal void RequestDismount()
        {
            SendDismountRequestAsync().Forget(LogRpcFault);
        }

        internal void ForceDismount()
        {
            _dismounted = true;
            _onRidingStateChanged.OnNext(Unit.Default);
        }

        // 操作対象の車両ID。TrainHUDはTrainCar ridableだけを扱う
        // The controlled car's id; the TrainHUD handles only TrainCar ridables
        internal bool TryGetRidingTrainCarId(out TrainCarInstanceId trainCarInstanceId)
        {
            trainCarInstanceId = default;
            if (_rideContext == null || !_rideContext.TryGetTarget(out var target)) return false;
            if (target.RidableType != RidableType.TrainCar) return false;
            trainCarInstanceId = new TrainCarInstanceId(target.TrainCarInstanceId);
            return true;
        }

        internal void Exit()
        {
            _eventSubscription?.Dispose();
            _eventSubscription = null;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _rideContext = null;
        }

        private async UniTask SendRideRequestAsync(RideTrainCarRequest rideRequest)
        {
            if (_cts != null)
            {
                Debug.LogWarning("[TrainHUDScreenState] 乗降要求が処理中のため乗車要求を保留します");
                return;
            }
            _cts = new CancellationTokenSource();

            // RPCが例外で抜けても要求中の印を必ず戻す。残すと以後の乗降要求が恒久的に保留される
            // Always clear the in-flight mark, even when the RPC throws; leaving it would hold every later ride request forever
            try
            {
                // 受理された座席を表示状態へ反映する
                // Apply the accepted seat to presentation state
                var target = RidableIdentifierMessagePack.CreateTrainCarMessage(rideRequest.TargetCarId.AsPrimitive());
                var response = await ClientContext.VanillaApi.Response.Train.RideAction(RideActionType.Ride, target, _cts.Token);
                if (response is { Result: RideActionResult.Success })
                {
                    _rideContext = new RidingPlayerStateContext(target, response.SeatIndex);
                    _playerStateController.SetState(PlayerStateEnum.Riding, _rideContext);
                    _onRidingStateChanged.OnNext(Unit.Default);
                }
                else
                {
                    ForceDismount();
                }
            }
            finally
            {
                ClearInFlightRequest();
            }
        }

        private async UniTask SendDismountRequestAsync()
        {
            if (_cts != null)
            {
                Debug.LogWarning("[TrainHUDScreenState] 乗降要求が処理中のため降車要求を保留します");
                return;
            }
            _cts = new CancellationTokenSource();
            try
            {
                var response = await ClientContext.VanillaApi.Response.Train.RideAction(RideActionType.Dismount, null, _cts.Token);
                if (response is { Result: RideActionResult.Success }) ForceDismount();
            }
            finally
            {
                ClearInFlightRequest();
            }
        }

        // 乗降要求の後始末はExit()と同じ形に揃える
        // The in-flight request teardown matches what Exit() does
        private void ClearInFlightRequest()
        {
            _cts?.Dispose();
            _cts = null;
        }

        private void OnRidingStateEventReceived(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(payload);
            // 他プレイヤーのイベントではローカルHUDを閉じない
            // Keep the local HUD open for another player's event
            if (message.PlayerId != ClientContext.PlayerConnectionSetting.PlayerId) return;
            if (message.StateType == RidingStateEventType.Dismount) ForceDismount();
        }

        private static void LogRpcFault(Exception exception)
        {
            Debug.LogWarning($"[TrainHUDScreenState] RPC fault: {exception}");
        }
    }
}
