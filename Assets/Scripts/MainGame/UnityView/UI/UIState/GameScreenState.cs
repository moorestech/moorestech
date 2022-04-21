using MainGame.Control.UI.UIState;
using MainGame.UnityView.Block;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.HotBar;

namespace MainGame.UnityView.UI.UIState
{
    public class GameScreenState : IUIState
    {
        private readonly MoorestechInputSettings _input;
        private readonly IBlockClickDetect _blockClickDetect;
        private readonly SelectHotBarControl _selectHotBarControl;

        public GameScreenState(MoorestechInputSettings input,IBlockClickDetect blockClickDetect,SelectHotBarControl selectHotBarControl)
        {
            _input = input;
            _blockClickDetect = blockClickDetect;
            _selectHotBarControl = selectHotBarControl;
        }

        public bool IsNext()
        {
            return _input.UI.OpenInventory.triggered || _input.UI.OpenMenu.triggered || 
                   IsClickOpenableBlock() || 
                   _input.UI.BlockDelete.triggered || _selectHotBarControl.IsClicked || 
                   _input.UI.HotBar.ReadValue<int>() != 0;
        }

        public UIStateEnum GetNext()
        {
            if (_input.UI.OpenInventory.triggered)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (_input.UI.OpenMenu.triggered)
            {
                return UIStateEnum.PauseMenu;
            }
            if (IsClickOpenableBlock())
            {
                return UIStateEnum.BlockInventory;
            }
            if (_input.UI.BlockDelete.triggered)
            {
                return UIStateEnum.DeleteBar;
            }
            if (_selectHotBarControl.IsClicked || _input.UI.HotBar.ReadValue<int>() != 0)
            {
                return UIStateEnum.BlockPlace;
            }


            return UIStateEnum.GameScreen;
        }

        public void OnEnter(UIStateEnum lastStateEnum) { }
        public void OnExit() { }

        private bool IsClickOpenableBlock()
        {
            if (_blockClickDetect.IsBlockClicked() && _blockClickDetect.GetClickedObject().GetComponent<OpenableInventoryBlock>())
            {
                return true;
            }

            return false;
        }
    }
}