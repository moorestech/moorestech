using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class GameScreenState : IUIState
    {
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly InGameCameraController _inGameCameraController;
        private readonly SkitManager _skitManager;
        
        public GameScreenState(IPlacementPreviewBlockGameObjectController previewBlockController, SkitManager skitManager, InGameCameraController inGameCameraController)
        {
            _previewBlockController = previewBlockController;
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;
            if (BlockClickDetect.IsClickOpenableBlock(_previewBlockController)) return UIStateEnum.BlockInventory;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Story;
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.PlaceBlock;
            if (UnityEngine.Input.GetKeyDown(KeyCode.T)) return UIStateEnum.ChallengeList;
            
            return UIStateEnum.Current;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            InputManager.MouseCursorVisible(false);
            _inGameCameraController.SetControllable(true);
            
            KeyControlDescription.Instance.SetText("Tab: インベントリ\n1~9: アイテム持ち替え\nB: ブロック配置\nG:ブロック削除\nT: チャレンジ一覧\n");
        }
        
        public void OnExit()
        {
            _inGameCameraController.SetControllable(false);
        }
    }
}