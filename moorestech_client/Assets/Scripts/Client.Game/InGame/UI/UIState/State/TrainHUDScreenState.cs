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
    // 列車に乗車中の HUD ステート。State が列車関連処理の唯一の起点である
    // Train HUD state, the single source of truth for all train-related processing while riding.
    public class TrainHUDScreenState : IUIState
    {
        private readonly PlayerStateController _playerStateController;
        private readonly InGameCameraController _inGameCameraController;
        private readonly TrainUnitClientCache _trainUnitClientCache;

        // 乗降状態の真実値を保持する context。RidingPlayerState はこの context のプロパティを毎フレーム読む。
        // Source-of-truth context for ride state; RidingPlayerState reads its properties each frame.
        private readonly RidingPlayerStateContext _rideContext = new();

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

        public void OnEnter(UITransitContext context)
        {
            _inGameCameraController.SetControllable(true);
            KeyControlDescription.Instance.SetText("E: 降車\nW/A/S/D: 列車操作\n");

            // サーバー強制降車イベントを購読する。HUD に居る間だけ反映する設計。
            // Subscribe to server-forced dismount events; only applied while this HUD is active.
            _eventSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(RidingStateEventPacket.EventTag, OnRidingStateEventReceived);

            var rideRequest = context?.GetContext<RideVehicleRequest>();
            if (rideRequest != null)
            {
                _rideContext.SetRideTarget(rideRequest.TargetCarId, -1);
                SendRideRequestAsync(rideRequest.TargetCarId).Forget(LogRpcFault);
            }
            // 2) ログイン復帰経路: PreparePendingRide で仕込まれた値を使う（座席既知）。
            // 2) Login-restore path: consume the value cached by PreparePendingRide (seat already known).
            else if (_pendingRideCarId.HasValue)
            {
                _rideContext.SetRideTarget(_pendingRideCarId.Value, _pendingRideSeatIndex);
                _pendingRideCarId = null;
                _pendingRideSeatIndex = -1;
            }

            // プレイヤーステートを Riding に押し出し、所有する context を渡す。
            // Push player state to Riding with the owned context.
            _playerStateController.SetState(PlayerStateEnum.Riding, _rideContext);

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
                    _rideContext.Clear();
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
                    // 成功なら座席番号付きで確定、失敗・例外・null 応答のいずれでも context をクリアして GameScreen に戻す。
                    // Finalize with seat index on success; clear and bounce to GameScreen on failure / exception / null response.
                    if (success)
                    {
                        _rideContext.SetRideTarget(carId, response.SeatIndex);
                    }
                    else
                    {
                        _rideContext.Clear();
                    }
                }
            }

            #endregion
        }

        public UITransitContext GetNextUpdate()
        {
            // context が空（RPC 失敗 / 強制降車 / 自発降車成功）なら GameScreen に戻る。
            // Bounce back to GameScreen when the context is empty (RPC failure / forced dismount / self-dismount success).
            if (!_rideContext.CurrentCarId.HasValue) return new UITransitContext(UIStateEnum.GameScreen);

            // 緊急退避用に PauseMenu のみ許可する。
            // Allow only the PauseMenu as an escape hatch.
            if (InputManager.UI.OpenMenu.GetKeyDown) return new UITransitContext(UIStateEnum.PauseMenu);

            // E で降車 RPC を fire-and-forget。次フレーム以降に空 context 経路で GameScreen へ。
            // Fire-and-forget dismount RPC on E; flips to GameScreen on the next frame via the empty-context path.
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                SendDismountRequestAsync().Forget(LogRpcFault);
            }

            // WASD 操舵入力を送信する。RPC 応答未到達 (seat=-1) のうちは送らない。
            // Forward WASD steering input. Skip while the seat is unconfirmed (seat=-1, RPC pending).
            if (_rideContext.CurrentSeatIndex >= 0)
            {
                SendWasdInput(_rideContext.CurrentCarId.Value);
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
                    // 成功なら context をクリアし次フレームで GameScreen へ。失敗時は維持（サーバー側で降車が起きていない可能性）。
                    // On success clear context and bounce next frame; on failure keep state (server may not have dismounted).
                    if (success)
                    {
                        _rideContext.Clear();
                    }
                }
            }

            void SendWasdInput(TrainCarInstanceId carId)
            {
                // 対象車両が cache から消えたら強制降車する（旧 TrainCarRidingInputSender から踏襲）。
                // Force dismount if the target car disappeared from the cache (mirrors prior TrainCarRidingInputSender).
                if (!_trainUnitClientCache.TryGetCarSnapshot(carId, out _, out _, out _, out _))
                {
                    _rideContext.Clear();
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

            _rideContext.Clear();
        }

        // fire-and-forget RPC の例外を UnobservedTaskException 経由 log のみに頼らず明示的に拾う。
        // Surface fire-and-forget RPC exceptions explicitly instead of relying on UnobservedTaskException.
        private static void LogRpcFault(Exception exception)
        {
            Debug.LogWarning($"[TrainHUDScreenState] RPC fault: {exception}");
        }
    }
}
