using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    // 列車に乗車中の HUD ステート。本 State が列車関連処理の唯一の起点であり、
    // 乗降状態の保持・RPC 送受信・サーバー強制降車購読・WASD 送信を一手に担う。
    // PlayerStateController には IPlayerRideContext として自分を渡し、RidingPlayerState は
    // 毎フレーム context 経由で現在乗るべき列車を問い合わせる。
    // HUD state while riding a train. This state is the sole entry point for train-related processing:
    // ride-state persistence, RPC send/receive, server-forced dismount subscription, and WASD input forwarding.
    // The state hands itself to PlayerStateController as IPlayerRideContext so RidingPlayerState can
    // query "the car to mount" through the context every frame.
    public class TrainHUDScreenState : IUIState, IPlayerRideContext
    {
        private readonly PlayerStateController _playerStateController;
        private readonly InGameCameraController _inGameCameraController;
        private readonly TrainUnitClientCache _trainUnitClientCache;

        // 現在乗車中の車両 id と座席 index。null / -1 は「乗車していない / RPC 応答未到達」。
        // Current riding car id and seat index. null / -1 means "not riding" / "RPC reply pending".
        private TrainCarInstanceId? _currentCarId;
        private int _currentSeatIndex = -1;

        // ログイン復帰時に MainGameStarter から push される保留中の乗車情報。
        // Pending ride info pushed from MainGameStarter on login restore.
        private TrainCarInstanceId? _pendingRideCarId;
        private int _pendingRideSeatIndex = -1;

        private IDisposable _eventSubscription;

        public TrainHUDScreenState(
            PlayerStateController playerStateController,
            InGameCameraController inGameCameraController,
            TrainUnitClientCache trainUnitClientCache)
        {
            _playerStateController = playerStateController;
            _inGameCameraController = inGameCameraController;
            _trainUnitClientCache = trainUnitClientCache;
        }

        // ハンドシェイク応答で乗車中だった場合、本 State の OnEnter で参照される pending 情報を仕込む。
        // Caches pending ride info from the handshake response, consumed by the next OnEnter.
        public void PreparePendingRide(TrainCarInstanceId carId, int seatIndex)
        {
            _pendingRideCarId = carId;
            _pendingRideSeatIndex = seatIndex;
        }

        // RidingPlayerState から毎フレーム呼ばれる context API。
        // Context API called every frame from RidingPlayerState.
        public bool TryGetCurrentRideTarget(out TrainCarInstanceId carId, out int seatIndex)
        {
            if (_currentCarId.HasValue && _currentSeatIndex >= 0)
            {
                carId = _currentCarId.Value;
                seatIndex = _currentSeatIndex;
                return true;
            }
            carId = default;
            seatIndex = -1;
            return false;
        }

        public void OnEnter(UITransitContext context)
        {
            _inGameCameraController.SetControllable(true);
            InputManager.MouseCursorVisible(false);
            KeyControlDescription.Instance.SetText("E: 降車\nW/A/S/D: 列車操作\n");

            // サーバー強制降車イベントを購読する。HUD に居る間だけ反映する設計。
            // Subscribe to server-forced dismount events; only applied while this HUD is active.
            _eventSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(RidingStateEventPacket.EventTag, OnRidingStateEventReceived);

            // 1) GameScreen からの E 入力経由: container から乗車要求を取り出し RPC を発射。
            // 1) Via E-press from GameScreen: pull the ride request from the container and fire the RPC.
            var rideRequest = context?.GetContext<RideVehicleRequest>();
            if (rideRequest != null)
            {
                _currentCarId = rideRequest.TargetCarId;
                _currentSeatIndex = -1;
                SendRideRequestAsync(rideRequest.TargetCarId).Forget(LogRpcFault);
            }
            // 2) ログイン復帰経路: PreparePendingRide で仕込まれた値を使う（座席既知）。
            // 2) Login-restore path: consume the value cached by PreparePendingRide (seat already known).
            else if (_pendingRideCarId.HasValue)
            {
                _currentCarId = _pendingRideCarId.Value;
                _currentSeatIndex = _pendingRideSeatIndex;
                _pendingRideCarId = null;
                _pendingRideSeatIndex = -1;
            }

            // プレイヤーステートを Riding に押し出し、自身を context として渡す。
            // Push player state to Riding with self as the context.
            _playerStateController.SetState(PlayerStateEnum.Riding, this);

            #region Internal

            // サーバー強制降車のみ反映する。Ride イベントは自分の RPC レスポンスで反映済みなので無視する（仕様§5.2）。
            // Apply only server-forced dismounts; Ride events are already applied via our RPC response (spec §5.2).
            void OnRidingStateEventReceived(byte[] payload)
            {
                if (payload == null || payload.Length == 0) return;
                var message = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(payload);
                if (message == null) return;
                if (message.PlayerId != ClientContext.PlayerConnectionSetting.PlayerId) return;

                if (message.StateType == RidingStateEventType.Dismount)
                {
                    _currentCarId = null;
                    _currentSeatIndex = -1;
                }
            }

            async UniTask SendRideRequestAsync(TrainCarInstanceId carId)
            {
                var target = RidableIdentifierMessagePack.CreateTrainCarMessage(carId.AsPrimitive());
                RideActionProtocol.ResponseRideActionMessagePack response = null;
                var success = false;
                try
                {
                    response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Ride, target, CancellationToken.None);
                    success = response != null && response.Result == RideActionResult.Success;
                }
                finally
                {
                    // 成功なら座席番号付きで確定、失敗・例外・null 応答のいずれでも内部状態をクリアして GameScreen に戻す。
                    // Finalize with seat index on success; clear and bounce to GameScreen on failure / exception / null response.
                    if (success)
                    {
                        _currentSeatIndex = response.SeatIndex;
                    }
                    else
                    {
                        _currentCarId = null;
                        _currentSeatIndex = -1;
                    }
                }
            }

            #endregion
        }

        public UITransitContext GetNextUpdate()
        {
            // 内部状態が空（RPC 失敗 / 強制降車 / 自発降車成功）なら GameScreen に戻る。
            // Bounce back to GameScreen when the internal state is empty (RPC failure / forced dismount / self-dismount success).
            if (!_currentCarId.HasValue) return new UITransitContext(UIStateEnum.GameScreen);

            // 緊急退避用に PauseMenu のみ許可する。
            // Allow only the PauseMenu as an escape hatch.
            if (InputManager.UI.OpenMenu.GetKeyDown) return new UITransitContext(UIStateEnum.PauseMenu);

            // E で降車 RPC を fire-and-forget。次フレーム以降に IsRiding=false 経路で GameScreen へ。
            // Fire-and-forget dismount RPC on E; flips to GameScreen on the next frame via the empty-state path.
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                SendDismountRequestAsync().Forget(LogRpcFault);
            }

            // WASD 操舵入力を送信する。RPC 応答未到達 (seat=-1) のうちは送らない。
            // Forward WASD steering input. Skip while the seat is unconfirmed (seat=-1, RPC pending).
            if (_currentSeatIndex >= 0)
            {
                SendWasdInput(_currentCarId.Value);
            }

            return null;

            #region Internal

            async UniTask SendDismountRequestAsync()
            {
                RideActionProtocol.ResponseRideActionMessagePack response = null;
                var success = false;
                try
                {
                    response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Dismount, null, CancellationToken.None);
                    success = response != null && response.Result == RideActionResult.Success;
                }
                finally
                {
                    // 成功なら内部状態をクリアし次フレームで GameScreen へ。失敗時はそのまま残す（サーバー側で降車が起きていない可能性）。
                    // On success clear internal state and bounce next frame; on failure keep state (server may not have dismounted).
                    if (success)
                    {
                        _currentCarId = null;
                        _currentSeatIndex = -1;
                    }
                }
            }

            void SendWasdInput(TrainCarInstanceId carId)
            {
                // 対象車両が cache から消えたら強制降車する（旧 TrainCarRidingInputSender から踏襲）。
                // Force dismount if the target car disappeared from the cache (mirrors prior TrainCarRidingInputSender).
                if (!_trainUnitClientCache.TryGetCarSnapshot(carId, out _, out _, out _, out _))
                {
                    _currentCarId = null;
                    _currentSeatIndex = -1;
                    return;
                }

                ClientContext.VanillaApi.SendOnly.SendTrainCarRidingInput(
                    carId,
                    UnityEngine.Input.GetKey(KeyCode.W),
                    UnityEngine.Input.GetKey(KeyCode.A),
                    UnityEngine.Input.GetKey(KeyCode.S),
                    UnityEngine.Input.GetKey(KeyCode.D));
            }

            #endregion
        }

        public void OnExit()
        {
            _inGameCameraController.SetControllable(false);

            _eventSubscription?.Dispose();
            _eventSubscription = null;

            // 降車処理 (parent 解除・pose ワープ) は RidingPlayerState.OnExit が担当する。
            // The dismount work (unparent / pose warp) is owned by RidingPlayerState.OnExit.
            _playerStateController.SetState(PlayerStateEnum.Normal, null);

            _currentCarId = null;
            _currentSeatIndex = -1;
        }

        // fire-and-forget RPC の例外を UnobservedTaskException 経由 log のみに頼らず明示的に拾う。
        // Surface fire-and-forget RPC exceptions explicitly instead of relying on UnobservedTaskException.
        private static void LogRpcFault(Exception exception)
        {
            Debug.LogWarning($"[TrainHUDScreenState] RPC fault: {exception}");
        }
    }
}
