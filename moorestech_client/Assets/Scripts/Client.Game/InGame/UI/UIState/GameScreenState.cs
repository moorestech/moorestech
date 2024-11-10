using Client.Game.GameDebug;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class GameScreenState : IUIState
    {
        private readonly IBlockPlacePreview _blockPlacePreview;
        private readonly InGameCameraController _inGameCameraController;
        private readonly SkitManager _skitManager;
        
        public GameScreenState(IBlockPlacePreview blockPlacePreview, SkitManager skitManager, InGameCameraController inGameCameraController)
        {
            _blockPlacePreview = blockPlacePreview;
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
        }
        
        public UIStateEnum GetNext()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;
            if (BlockClickDetect.IsClickOpenableBlock(_blockPlacePreview)) return UIStateEnum.BlockInventory;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Story;
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.PlaceBlock;
            if (DebugInfoStore.EnableBlockDebugMode) return UIStateEnum.BlockDebug;
            
            return UIStateEnum.Current;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            InputManager.MouseCursorVisible(false);
            _inGameCameraController.SetControllable(true);
        }
        
        public void OnExit()
        {
            _inGameCameraController.SetControllable(false);
        }
    }
}