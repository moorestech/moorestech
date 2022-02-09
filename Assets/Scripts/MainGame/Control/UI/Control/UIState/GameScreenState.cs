using MainGame.Control.Game;

namespace MainGame.Control.UI.Control.UIState
{
    public class GameScreenState : IUIState
    {
        private MoorestechInputSettings _input;
        private IBlockClickDetect _blockClickDetect;
        private IUIState _inventoryState;
        private IUIState _pauseState;
        private IUIState _blockInventoryState;
        public void Construct(
            IUIState inventoryState, IUIState pauseState,IUIState blockInventoryState,
            MoorestechInputSettings input,IBlockClickDetect blockClickDetect)
        {
            _input = input;
            _inventoryState = inventoryState;
            _pauseState = pauseState;
            _blockClickDetect = blockClickDetect;
            _blockInventoryState = blockInventoryState;
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

            return false;
        }

        public IUIState GetNext()
        {
            if (_input.UI.OpenInventory.triggered)
            {
                return _inventoryState;
            }
            if (_input.UI.OpenMenu.triggered)
            {
                return _pauseState;
            }

            if (_blockClickDetect.IsBlockClicked())
            {
                return _blockInventoryState;
            }


            return this;
        }

        public void OnEnter() { }
        public void OnExit() { }
    }
}