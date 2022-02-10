using UnityEngine;

namespace MainGame.Control.UI.UIState.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly IUIState _gameScreen;
        private readonly MoorestechInputSettings _inputSettings;
        private readonly GameObject _blockInventory;

        public BlockInventoryState(IUIState gameScreen, MoorestechInputSettings inputSettings, GameObject blockInventory)
        {
            _gameScreen = gameScreen;
            _inputSettings = inputSettings;
            _blockInventory = blockInventory;
            blockInventory.SetActive(false);
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered;
        }

        public IUIState GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered)
            {
                return _gameScreen;
            }

            return this;
        }

        public void OnEnter() { _blockInventory.SetActive(true); }

        public void OnExit() { _blockInventory.SetActive(false); }
    }
}