using Client.Game.BlockSystem;
using Client.Game.Control.MouseKeyboard;
using Client.Game.Story;
using MainGame.UnityView.Control;
using UnityEngine;

namespace Client.Game.UI.UIState
{
    public class GameScreenState : IUIState
    {
        private readonly PlayerStoryStarter _playerStoryStarter;
        private readonly IBlockPlacePreview _blockPlacePreview;

        public GameScreenState(PlayerStoryStarter playerStoryStarter, IBlockPlacePreview blockPlacePreview)
        {
            _playerStoryStarter = playerStoryStarter;
            _blockPlacePreview = blockPlacePreview;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;
            if (IsClickOpenableBlock()) return UIStateEnum.BlockInventory;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (_playerStoryStarter.IsStartReady && Input.GetKeyDown(KeyCode.F)) return UIStateEnum.Story; //TODO インプットマネージャー整理

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            InputManager.MouseCursorVisible(false);
        }

        public void OnExit()
        {
        }

        private bool IsClickOpenableBlock()
        {
            if (_blockPlacePreview.IsActive) return false; //ブロック設置中の場合は無効
            if (BlockClickDetect.TryGetClickBlock(out var block)) return block.GetComponent<OpenableInventoryBlock>();

            return false;
        }
    }
}