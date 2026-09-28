using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.InGame.UI.UIState.State.TrainHUDScreen;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State
{
    // 列車に乗車中の HUD ステート。状態遷移と列車系UI処理の呼び出しを担当する
    // Train HUD state, responsible for state transitions and dispatching train UI work while riding.
    public class TrainHUDScreenState : IUIState, IApplicationFocusRestorer, INestedPauseScreenState
    {
        private readonly PlayerStateController _playerStateController;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly NestedPauseSubStateController _subStateController;
        private readonly TrainRidingInputSender _trainRidingInputSender = new();
        private readonly TrainBranchRoutePreviewController _branchRoutePreviewController = new();

        private readonly TrainHudRideSession _rideSession;
        private readonly Subject<Unit> _onPresentationChanged = new();
        private int _lastBranchCandidateCount;

        public bool IsRiding => _rideSession.RideContext != null && !_rideSession.IsDismountTrain;
        public NestedPauseSubStateEnum SubState => _subStateController.CurrentState;
        public int BranchCandidateCount => _branchRoutePreviewController.BranchCandidateCount;
        public int SelectedBranchIndex { get; private set; }
        public IObservable<Unit> OnPresentationChanged => _onPresentationChanged;


        public TrainHUDScreenState(PlayerStateController playerStateController, TrainUnitClientCache trainUnitClientCache, InGameCameraController inGameCameraController, PauseMenuStateService pauseMenuStateService)
        {
            _playerStateController = playerStateController;
            _rideSession = new TrainHudRideSession(playerStateController);
            _rideSession.OnChanged.Subscribe(_ => _onPresentationChanged.OnNext(Unit.Default));
            _trainUnitClientCache = trainUnitClientCache;
            _subStateController = new NestedPauseSubStateController(new TrainHudGameScreenSubState(inGameCameraController), pauseMenuStateService);
            _subStateController.OnStateChanged.Subscribe(_ => _onPresentationChanged.OnNext(Unit.Default));
        }

        public void OnEnter(UITransitContext context)
        {
            _subStateController.StartSubState();
            _trainRidingInputSender.Reset();
            _rideSession.Enter(context);
        }

        public UITransitContext GetNextUpdate()
        {
            if (_rideSession.IsDismountTrain)
            {
                return new UITransitContext(UIStateEnum.GameScreen);
            }

            // まだ乗車が完了していないのであれば何もしない
            // If riding is not yet completed, do nothing.
            if (_rideSession.RideContext == null) return null;

            // 対象車両が消えたら強制降車
            // Force dismount if the target car has disappeared.
            if (!TryGetRidingTrainCarId(out var ridingTrainCarId) || !_trainUnitClientCache.TryGetCarSnapshot(ridingTrainCarId, out var ridingTrainUnit, out _, out _, out _))
            {
                _rideSession.ForceDismount();
                return new UITransitContext(UIStateEnum.GameScreen);
            }
                
            // TrainHUD内部のサブUIステートを実行
            // Run the nested sub-state for the Train HUD.
            _subStateController.Update();
            if (_subStateController.CurrentState != NestedPauseSubStateEnum.GameScreen)
            {
                _branchRoutePreviewController.Hide();
                return null;
            }

            _branchRoutePreviewController.Update(ridingTrainUnit);
            var selectedBranchIndex = ridingTrainUnit.GetManualBranchSelectionIndex();
            var branchCandidateCount = _branchRoutePreviewController.BranchCandidateCount;
            if (SelectedBranchIndex != selectedBranchIndex || _lastBranchCandidateCount != branchCandidateCount)
            {
                SelectedBranchIndex = selectedBranchIndex;
                _lastBranchCandidateCount = branchCandidateCount;
                _onPresentationChanged.OnNext(Unit.Default);
            }

            // GameScreenだけ降車処理、列車操作入力を受け付け
            // Only process dismount and train control input on the GameScreen.
            if (HybridInput.GetKeyDown(KeyCode.E))
            {
                _rideSession.RequestDismount();
            }

            _trainRidingInputSender.Update();

            return null;

        }

        public void OnExit()
        {
            _rideSession.Exit();

            // 入れ子サブステートを終了（必要に応じてポーズメニューを閉じる）
            // Tear down the nested sub-state (closes the pause menu if it is open).
            _subStateController.ShutdownSubState();

            // ステートを変更して降車処理を実行
            // Change state to trigger dismount processing.
            _playerStateController.SetState(PlayerStateEnum.Normal, null);
            _branchRoutePreviewController.Destroy();
            SelectedBranchIndex = 0;
            _onPresentationChanged.OnNext(Unit.Default);

        }
        
        public void RestoreAfterApplicationFocus()
        {
            _subStateController.RestoreAfterApplicationFocus();
        }

        // 表示中のサブステートが宣言したヒントをそのまま返す
        // Return the hints declared by whichever sub-state is currently showing
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return _subStateController.GetKeyHints();
        }

        public bool RequestClosePauseMenu()
        {
            return _subStateController.RequestClosePauseMenu();
        }

        private bool TryGetRidingTrainCarId(out TrainCarInstanceId trainCarInstanceId)
        {
            trainCarInstanceId = default;
            if (_rideSession.RideContext == null || !_rideSession.RideContext.TryGetTarget(out var target))
            {
                return false;
            }

            // TrainHUD は TrainCar ridable だけを操作対象として扱う
            // TrainHUD handles only TrainCar ridables as controllable targets
            if (target.RidableType != RidableType.TrainCar)
            {
                return false;
            }
            trainCarInstanceId = new TrainCarInstanceId(target.TrainCarInstanceId);
            return true;
        }
    }
}
