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
            return InputManager.Settings.UI.OpenInventory.triggered || InputManager.Settings.UI.OpenMenu.triggered || 
                   IsClickOpenableBlock() || 
                   InputManager.Settings.UI.BlockDelete.triggered || _selectHotBarControl.IsClicked || 
                   InputManager.Settings.UI.HotBar.ReadValue<int>() != 0 || InputManager.Settings.UI.QuestUI.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.Settings.UI.OpenInventory.triggered)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (InputManager.Settings.UI.OpenMenu.triggered)
            {
                return UIStateEnum.PauseMenu;
            }
            if (IsClickOpenableBlock())
            {
                return UIStateEnum.BlockInventory;
            }
            if (InputManager.Settings.UI.BlockDelete.triggered)
            {
                return UIStateEnum.DeleteBar;
            }
            if (_selectHotBarControl.IsClicked || InputManager.Settings.UI.HotBar.ReadValue<int>() != 0)
            {
                return UIStateEnum.BlockPlace;
            }
            if (InputManager.Settings.UI.QuestUI.triggered)
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