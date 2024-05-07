using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Control;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class PlaceBlockState : IUIState
    {
        private readonly IBlockPlacePreview _blockPlacePreview;
        private readonly InGameCameraController _inGameCameraController;
        private readonly SkitManager _skitManager;
        public PlaceBlockState(IBlockPlacePreview blockPlacePreview, SkitManager skitManager, InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
            _skitManager = skitManager;
            _blockPlacePreview = blockPlacePreview;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            InputManager.MouseCursorVisible(true);
        }
        public UIStateEnum GetNext()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;
            if (IsClickOpenableBlock()) return UIStateEnum.BlockInventory;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Story;
            //TODO InputSystemのリファクタ対象
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.GameScreen;
            
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                InputManager.MouseCursorVisible(false);
                _inGameCameraController.updateCameraAngle = true;
            }
            if (UnityEngine.Input.GetMouseButtonUp(1))
            {
                InputManager.MouseCursorVisible(true);
                _inGameCameraController.updateCameraAngle = false;
            }
            
            return UIStateEnum.Current;
        }
        public void OnExit()
        {
            InputManager.MouseCursorVisible(false);
        }
        
        private bool IsClickOpenableBlock()
        {
            if (_blockPlacePreview.IsActive) return false; //ブロック設置中の場合は無効
            if (BlockClickDetect.TryGetClickBlock(out var block)) return block.GetComponent<OpenableInventoryBlock>();
            
            return false;
        }
    }
}