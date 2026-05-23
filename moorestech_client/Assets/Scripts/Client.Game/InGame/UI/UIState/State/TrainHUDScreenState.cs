using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Cysharp.Threading.Tasks;
using Game.PlayerRiding.Interface;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    // 列車に乗車中の HUD ステート。State が列車関連処理の唯一の起点である
    // Train HUD state, the single source of truth for all train-related processing while riding.
    public class TrainHUDScreenState : IUIState
    {
        private readonly PlayerStateController _playerStateController;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly InGameCameraController _inGameCameraController;
        private readonly PauseMenuStateService _pauseMenuStateService;
        
        private bool _isDismountTrain = false;
        private RidingPlayerStateContext _rideContext;
        
        private IDisposable _eventSubscription;
        private CancellationTokenSource _cts;
        

        public TrainHUDScreenState(PlayerStateController playerStateController, TrainUnitClientCache trainUnitClientCache, InGameCameraController inGameCameraController, PauseMenuStateService pauseMenuStateService)
        {
            _playerStateController = playerStateController;
            _trainUnitClientCache = trainUnitClientCache;
            _inGameCameraController = inGameCameraController;
            _pauseMenuStateService = pauseMenuStateService;
        }

        public void OnEnter(UITransitContext context)
        {
            _inGameCameraController.SetControllable(true);
            KeyControlDescription.Instance.SetText("E: 降車\nW/A/S/D: 列車操作\n");
            
            // サーバー強制降車イベントを購読する。HUDに居る間だけ反映
            // Subscribe to server-forced dismount events; only applied while this HUD is active.
            _eventSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(RidingStateEventPacket.EventTag, OnRidingStateEventReceived);
            
            // 初期値として乗車完了済みの場合は即時反映
            // If the player is already riding at the time of entering, reflect that immediately.
            if (context.TryGetContext<InitialRideTrainCarRequest>(out var rideRequest))
            {
                _rideContext = new RidingPlayerStateContext(rideRequest.TargetCarId, rideRequest.SeatIndex);
                _playerStateController.SetState(PlayerStateEnum.Riding, _rideContext);
                return;
            }

            // サーバー側に乗車リクエストを送る
            // Send a ride request to the server.
            _rideContext = null;
            _isDismountTrain = false;
            SendRideRequestAsync().Forget(LogRpcFault);


            #region Internal
            
            async UniTask SendRideRequestAsync()
            {
                if(_cts  != null) return;
                
                var rideRequest = context.GetContext<RideTrainCarRequest>();
                
                _cts  = new CancellationTokenSource();
                var target = RidableIdentifierMessagePack.CreateTrainCarMessage(rideRequest.TargetCarId.AsPrimitive());
                var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Ride, target, _cts.Token);
                
                if (response is { Result: RideActionResult.Success })
                {
                    // 乗車を実行
                    // Execute riding.
                    _rideContext = new RidingPlayerStateContext(rideRequest.TargetCarId, response.SeatIndex);
                    _playerStateController.SetState(PlayerStateEnum.Riding, _rideContext);
                }
                else
                {
                    // 乗車できなかったのでGameScreenに戻る
                    // Failed to ride, bounce back
                    _isDismountTrain = true;
                }
                
                _cts = null;
            }
                
            // サーバー起因の強制降車を反映
            // Reflect server-forced dismounts.
            void OnRidingStateEventReceived(byte[] payload)
            {
                var message = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(payload);
                if (message.PlayerId != ClientContext.PlayerConnectionSetting.PlayerId) return;

                // 降車とゲームスクリーンへの遷移
                // Dismount and transition to GameScreen.
                if (message.StateType == RidingStateEventType.Dismount)
                {
                    _playerStateController.SetState(PlayerStateEnum.Normal, null);
                    _isDismountTrain = true;
                }
            }

            #endregion
        }

        public UITransitContext GetNextUpdate()
        {
            if (_isDismountTrain) return new UITransitContext(UIStateEnum.GameScreen);
            
            // まだ乗車が完了していないのであれば何もしない
            // If riding is not yet completed, do nothing.
            if (_rideContext == null) return null;
            
            // 対象車両が消えたら強制降車
            // Force dismount if the target car has disappeared.
            if (!_trainUnitClientCache.TryGetCarSnapshot( _rideContext.CurrentCarId, out _, out _, out _, out _))  return new UITransitContext(UIStateEnum.GameScreen);
            
            
            // 戻る操作のリクエスト
            // Request dismount
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                SendDismountRequestAsync().Forget(LogRpcFault);
            }
            
            // 列車操作入力を送る
            // Send train control inputs.
            SendWasdInput();

            return null;

            #region Internal

            async UniTask SendDismountRequestAsync()
            {
                if (_cts != null) return;
                
                _cts  = new CancellationTokenSource();
                
                var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Dismount, null, _cts.Token);
                if (response is { Result: RideActionResult.Success })
                {
                    // 降車したので GameScreen へ
                    // Successfully dismounted, transition to GameScreen.
                    _isDismountTrain = true;
                }
                
                _cts = null;
            }

            void SendWasdInput()
            {
                ClientContext.VanillaApi.SendOnly.SendTrainCarRidingInput(
                    _rideContext.CurrentCarId,
                    UnityEngine.Input.GetKey(KeyCode.W),
                    UnityEngine.Input.GetKey(KeyCode.A),
                    UnityEngine.Input.GetKey(KeyCode.S),
                    UnityEngine.Input.GetKey(KeyCode.D));
            }

            #endregion
        }

        public void OnExit()
        {
            _eventSubscription?.Dispose();
            _eventSubscription = null;
            
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _rideContext = null;

            // ステートを変更して降車処理を実行
            // Change state to trigger dismount processing.
            _playerStateController.SetState(PlayerStateEnum.Normal, null);

        }

        // fire-and-forget RPC の例外を UnobservedTaskException 経由 log のみに頼らず明示的に拾う。
        // Surface fire-and-forget RPC exceptions explicitly instead of relying on UnobservedTaskException.
        private static void LogRpcFault(Exception exception)
        {
            Debug.LogWarning($"[TrainHUDScreenState] RPC fault: {exception}");
        }
    }
}
