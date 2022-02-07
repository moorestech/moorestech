namespace MainGame.Control.UI.Control.UIState
{
    public class GameScreen : IUIState
    {
        private MoorestechInputSettings _input;
        private IUIState _inventoryState;
        private IUIState _pauseState;
        public void Construct(IUIState inventoryState, IUIState pauseState,MoorestechInputSettings input)
        {
            _input = input;
            _inventoryState = inventoryState;
            _pauseState = pauseState;
        }

        public bool IsNext()
        {
            if (_input.UI.OpenInventory.triggered)
            {
                return true;
            }else if (_input.UI.OpenMenu.triggered)
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

            return this;
        }

        public void OnEnter() { }
        public void OnExit() { }
    }
}