using MainGame.UnityView.Block;
using MainGame.UnityView.Control;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View.HotBar;

namespace MainGame.UnityView.UI.UIState
{
    public class GameScreenState : IUIState
    {
        private readonly IBlockClickDetect _blockClickDetect;
        private readonly SelectHotBarControl _selectHotBarControl;

        public GameScreenState(IBlockClickDetect blockClickDetect,SelectHotBarControl selectHotBarControl)
        {
            _blockClickDetect = blockClickDetect;
            _selectHotBarControl = selectHotBarControl;
        }

        public bool IsNext()
        {
            return InputManager.UI.OpenInventory.GetKey || InputManager.UI.OpenMenu.GetKey || 
                   IsClickOpenableBlock() || 
                   InputManager.UI.BlockDelete.GetKey || _selectHotBarControl.IsClicked || 
                   InputManager.UI.HotBar.ReadValue<int>() != 0 || InputManager.UI.QuestUI.GetKey;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.OpenInventory.GetKey)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (InputManager.UI.OpenMenu.GetKey)
            {
                return UIStateEnum.PauseMenu;
            }
            if (IsClickOpenableBlock())
            {
                return UIStateEnum.BlockInventory;
            }
            if (InputManager.UI.BlockDelete.GetKey)
            {
                return UIStateEnum.DeleteBar;
            }
            if (_selectHotBarControl.IsClicked || InputManager.UI.HotBar.ReadValue<int>() != 0)
            {
                return UIStateEnum.BlockPlace;
            }
            if (InputManager.UI.QuestUI.GetKey)
            {
                return UIStateEnum.QuestViewer;
            }


            return UIStateEnum.GameScreen;
        }

        public void OnEnter(UIStateEnum lastStateEnum) { }
        public void OnExit() { }

        private bool IsClickOpenableBlock()
        {
            if (_blockClickDetect.TryGetClickBlock(out var block))
            {
                return block.GetComponent<OpenableInventoryBlock>();
            }

            return false;
        }
    }
}