using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class GameScreenState : IUIState, IApplicationFocusRestorer
    {
        private readonly SkitManager _skitManager;
        private readonly GameScreenSubInventoryInteractService _subInventoryInteractService;
        private readonly RideVehicleInputService _rideVehicleInputService;
        private readonly PlacementTargetPickService _placementTargetPickService;
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;

        public GameScreenState(
            SkitManager skitManager,
            GameScreenSubInventoryInteractService subInventoryInteractService,
            RideVehicleInputService rideVehicleInputService,
            PlacementTargetPickService placementTargetPickService,
            IPlayerCameraInteractionApplier cameraInteractionApplier)
        {
            _skitManager = skitManager;
            _subInventoryInteractService = subInventoryInteractService;
            _rideVehicleInputService = rideVehicleInputService;
            _placementTargetPickService = placementTargetPickService;
            _cameraInteractionApplier = cameraInteractionApplier;
        }

        public UITransitContext GetNextUpdate()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
            if (InputManager.UI.OpenMenu.GetKeyDown) return new UITransitContext(UIStateEnum.PauseMenu);

            // 列車に乗り込む範囲＋E押下を 1 行で判定し、TrainHUDScreen へ遷移する。
            // One-line check for "in ride range + interact key pressed", transits to TrainHUDScreen.
            if (_rideVehicleInputService.TryGetInteractTransit(out var rideContext)) return rideContext;

            // ブロックや列車とインタラクトしたか
            if (_subInventoryInteractService.TryGetSubInventoryInteractObject(out var context)) return context;

            // ミドルクリックで設置物をスポイトし配置モードへ入る
            // Middle-click eyedrops a placed object and enters placement mode
            if (_placementTargetPickService.TryPickTargetUnderCursor(out var pickedTarget))
                return new UITransitContext(UIStateEnum.PlaceBlock, UITransitContextContainer.Create<IPlacementTarget>(pickedTarget));

            if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.DeleteBar);
            if (_skitManager.IsPlayingSkit) return new UITransitContext(UIStateEnum.Story);
            
            //TODO InputSystemのリファクタ対象
            if (HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.BuildMenu);
            if (HybridInput.GetKeyDown(KeyCode.T)) return new UITransitContext(UIStateEnum.ChallengeList);
            if (HybridInput.GetKeyDown(KeyCode.R)) return new UITransitContext(UIStateEnum.ResearchTree);
            if (HybridInput.GetKeyDown(KeyCode.F3)) return new UITransitContext(UIStateEnum.Debug);

            return null;
        }

        public void OnEnter(UITransitContext context)
        {
            // 通常操作用にカーソルをロックし、視点回転を有効にする
            // Lock the cursor and enable look rotation for normal gameplay
            _cameraInteractionApplier.SetCursorVisible(false);
            _cameraInteractionApplier.SetCameraRotatable(true);

            // 旧uGUIのHUD表示をGameScreen復帰時に同期する
            // Sync legacy uGUI HUD visibility when returning to GameScreen.
            GameStateController.ChangeState(GameStateType.InGame);

            KeyControlDescription.Instance.SetText("Tab: インベントリ\n1~9: アイテム持ち替え\nV: 視点切替\nB: ブロック配置\nG:ブロック削除\nミドルクリック: 設置物をスポイト\nT: チャレンジ一覧\nR: リサーチツリー\nF3: デバッグモード\n");
        }

        public void OnExit()
        {
            // 次のUIが背後のカメラ回転を継承しないよう停止する
            // Stop look rotation so the next UI does not inherit background camera movement
            _cameraInteractionApplier.SetCameraRotatable(false);
        }

        public void RestoreAfterApplicationFocus()
        {
            _cameraInteractionApplier.SetCursorVisible(false);
            _cameraInteractionApplier.SetCameraRotatable(true);
        }
    }
}
