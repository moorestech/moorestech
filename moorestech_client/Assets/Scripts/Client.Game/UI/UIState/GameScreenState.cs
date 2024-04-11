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
        
        public GameScreenState(PlayerStoryStarter playerStoryStarter)
        {
            _playerStoryStarter = playerStoryStarter;
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
            if (BlockClickDetect.TryGetClickBlock(out var block)) return block.GetComponent<OpenableInventoryBlock>();

            return false;
        }
    }
}