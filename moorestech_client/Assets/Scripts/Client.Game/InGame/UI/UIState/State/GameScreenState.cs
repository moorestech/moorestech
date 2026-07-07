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

        public GameScreenState(
            SkitManager skitManager,
            InGameCameraController inGameCameraController,
            GameScreenSubInventoryInteractService subInventoryInteractService,
            RideVehicleInputService rideVehicleInputService)
        {
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
            _subInventoryInteractService = subInventoryInteractService;
            _rideVehicleInputService = rideVehicleInputService;
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
