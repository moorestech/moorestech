using Client.Game.InGame.Control;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class GameScreenState : IUIState
    {
        private readonly InGameCameraController _inGameCameraController;
        private readonly SkitManager _skitManager;
        private readonly GameScreenSubInventoryInteractService _subInventoryInteractService;
        private readonly RideVehicleInputService _rideVehicleInputService;
        private readonly TrainCarRidingState _trainCarRidingState;

        public GameScreenState(
            SkitManager skitManager,
            InGameCameraController inGameCameraController,
            GameScreenSubInventoryInteractService subInventoryInteractService,
            RideVehicleInputService rideVehicleInputService,
            TrainCarRidingState trainCarRidingState)
        {
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
            _subInventoryInteractService = subInventoryInteractService;
            _rideVehicleInputService = rideVehicleInputService;
            _trainCarRidingState = trainCarRidingState;
        }

        public UITransitContext GetNextUpdate()
        {
            // ログイン復帰 / PauseMenu 戻り等で乗車状態を背負ったまま GameScreen に居る場合は TrainHUDScreen へ。
            // 自発的 E 入力以外（ハンドシェイク復帰・サーバー強制乗車）にも対応するため最優先で判定する。
            // If we landed on GameScreen while already riding (login restore, PauseMenu return etc.), go to TrainHUDScreen.
            // Evaluated first so non-input ride sources (handshake restore, server-forced ride) are covered too.
            if (_trainCarRidingState.IsRiding) return new UITransitContext(UIStateEnum.TrainHUDScreen);

            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
            if (InputManager.UI.OpenMenu.GetKeyDown) return new UITransitContext(UIStateEnum.PauseMenu);

            // 列車に乗り込む範囲＋E押下を 1 行で判定し、TrainHUDScreen へ遷移する。
            // One-line check for "in ride range + interact key pressed", transits to TrainHUDScreen.
            if (_rideVehicleInputService.TryGetInteractTransit(out var rideContext)) return rideContext;

            // ブロックや列車とインタラクトしたか
            if (_subInventoryInteractService.TryGetSubInventoryInteractObject(out var context)) return context;

            if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.DeleteBar);
            if (_skitManager.IsPlayingSkit) return new UITransitContext(UIStateEnum.Story);
            
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.PlaceBlock);
            if (UnityEngine.Input.GetKeyDown(KeyCode.T)) return new UITransitContext(UIStateEnum.ChallengeList);
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) return new UITransitContext(UIStateEnum.ResearchTree);
            if (UnityEngine.Input.GetKeyDown(KeyCode.F3)) return new UITransitContext(UIStateEnum.Debug);

            return null;
        }

        public void OnEnter(UITransitContext context)
        {
            InputManager.MouseCursorVisible(false);
            _inGameCameraController.SetControllable(true);

            KeyControlDescription.Instance.SetText("Tab: インベントリ\n1~9: アイテム持ち替え\nB: ブロック配置\nG:ブロック削除\nT: チャレンジ一覧\nR: リサーチツリー\nF3: デバッグモード\n");
        }
        
        public void OnExit()
        {
            _inGameCameraController.SetControllable(false);
        }
    }
}
