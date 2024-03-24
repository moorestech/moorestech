using Client.Game.BlockSystem;
using Client.Game.Control.MouseKeyboard;
using MainGame.UnityView.Control;

namespace Client.Game.UI.UIState
{
    public class GameScreenState : IUIState
    {
        public UIStateEnum GetNext()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;
            if (IsClickOpenableBlock()) return UIStateEnum.BlockInventory;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;


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