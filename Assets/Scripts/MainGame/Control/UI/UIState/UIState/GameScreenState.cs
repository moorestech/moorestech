using MainGame.Control.Game.MouseKeyboard;
using MainGame.Control.UI.Inventory;

namespace MainGame.Control.UI.UIState.UIState
{
    public class GameScreenState : IUIState
    {
        private MoorestechInputSettings _input;
        private IBlockClickDetect _blockClickDetect;
        private readonly SelectHotBarControl _selectHotBarControl;

        public GameScreenState(MoorestechInputSettings input,IBlockClickDetect blockClickDetect,SelectHotBarControl selectHotBarControl)
        {
            _input = input;
            _blockClickDetect = blockClickDetect;
            _selectHotBarControl = selectHotBarControl;
        }

        public bool IsNext()
        {
            if (_input.UI.OpenInventory.triggered)
            {
                return true;
            }
            if (_input.UI.OpenMenu.triggered)
            {
                return true;
            }

            if (_blockClickDetect.IsBlockClicked())
            {
                return true;
            }
            if (_input.UI.BlockDelete.triggered)
            {
                return true;
            }

            if (_selectHotBarControl.IsClicked)
            {
                return true;
            }

            return false;
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
            if (_blockClickDetect.IsBlockClicked())
            {
                return UIStateEnum.BlockInventory;
            }
            if (_input.UI.BlockDelete.triggered)
            {
                return UIStateEnum.DeleteBar;
            }
            if (_selectHotBarControl.IsClicked)
            {
                return UIStateEnum.BlockPlace;
            }


            return UIStateEnum.GameScreen;
        }

        public void OnEnter() { }
        public void OnExit() { }
    }
}