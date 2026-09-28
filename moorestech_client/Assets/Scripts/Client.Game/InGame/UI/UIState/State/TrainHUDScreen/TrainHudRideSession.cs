using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Train.Unit;
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
        private readonly Subject<Unit> _onChanged = new();
        private IDisposable _eventSubscription;
        private CancellationTokenSource _cts;
        internal RidingPlayerStateContext RideContext { get; private set; }
        internal bool IsDismountTrain { get; private set; }
        internal IObservable<Unit> OnChanged => _onChanged;

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
            RideContext = null;
            IsDismountTrain = false;
            if (context.TryGetContext<InitialRideTrainCarRequest>(out var rideRequest))
            {
                // 既に乗車済みならサーバーへの再要求を行わない
                // Avoid another server request when the player is already riding
                var target = RidableIdentifierMessagePack.CreateTrainCarMessage(rideRequest.TargetCarId.AsPrimitive());
                RideContext = new RidingPlayerStateContext(target, rideRequest.SeatIndex);
                _playerStateController.SetState(PlayerStateEnum.Riding, RideContext);
                _onChanged.OnNext(Unit.Default);
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
            IsDismountTrain = true;
            _onChanged.OnNext(Unit.Default);
        }

        internal void Exit()
        {
            _eventSubscription?.Dispose();
            _eventSubscription = null;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            RideContext = null;
        }

        private async UniTask SendRideRequestAsync(RideTrainCarRequest rideRequest)
        {
            if (_cts != null)
            {
                Debug.LogWarning("[TrainHUDScreenState] 乗降要求が処理中のため乗車要求を保留します");
                return;
            }
            _cts = new CancellationTokenSource();
            // 受理された座席を表示状態へ反映する
            // Apply the accepted seat to presentation state
            var target = RidableIdentifierMessagePack.CreateTrainCarMessage(rideRequest.TargetCarId.AsPrimitive());
            var response = await ClientContext.VanillaApi.Response.Train.RideAction(RideActionType.Ride, target, _cts.Token);
            if (response is { Result: RideActionResult.Success })
            {
                RideContext = new RidingPlayerStateContext(target, response.SeatIndex);
                _playerStateController.SetState(PlayerStateEnum.Riding, RideContext);
                _onChanged.OnNext(Unit.Default);
            }
            else
            {
                ForceDismount();
            }
            _cts = null;
        }

        private async UniTask SendDismountRequestAsync()
        {
            if (_cts != null)
            {
                Debug.LogWarning("[TrainHUDScreenState] 乗降要求が処理中のため降車要求を保留します");
                return;
            }
            _cts = new CancellationTokenSource();
            var response = await ClientContext.VanillaApi.Response.Train.RideAction(RideActionType.Dismount, null, _cts.Token);
            if (response is { Result: RideActionResult.Success }) ForceDismount();
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
