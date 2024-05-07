using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Control;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class GameScreenState : IUIState
    {
        private readonly IBlockPlacePreview _blockPlacePreview;
        private readonly SkitManager _skitManager;
        
        public GameScreenState(IBlockPlacePreview blockPlacePreview, SkitManager skitManager)
        {
            _blockPlacePreview = blockPlacePreview;
            _skitManager = skitManager;
        }
        
        public UIStateEnum GetNext()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;
            if (IsClickOpenableBlock()) return UIStateEnum.BlockInventory;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Story;
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.PlaceBlock;
            
            return UIStateEnum.Current;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            InputManager.MouseCursorVisible(false);
        }
        
        public void OnExit() { }
        
        private bool IsClickOpenableBlock()
        {
            if (_blockPlacePreview.IsActive) return false; //ブロック設置中の場合は無効
            if (BlockClickDetect.TryGetClickBlock(out var block)) return block.GetComponent<OpenableInventoryBlock>();
            
            return false;
        }
    }
}