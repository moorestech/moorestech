using MainGame.Control.Game.MouseKeyboard;

namespace MainGame.Control.UI.UIState.UIState
{
    public class GameScreenState : IUIState
    {
        private MoorestechInputSettings _input;
        private IBlockClickDetect _blockClickDetect;
        public GameScreenState(MoorestechInputSettings input,IBlockClickDetect blockClickDetect)
        {
            _input = input;
            _blockClickDetect = blockClickDetect;
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


            return UIStateEnum.GameScreen;
        }

        public void OnEnter() { }
        public void OnExit() { }
    }
}