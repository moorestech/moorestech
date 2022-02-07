using UnityEngine;

namespace MainGame.Control.UI
{
    public class PlayerInventory : IUIState
    {
        private IUIState _gameScreen;
        private readonly MoorestechInputSettings _inputSettings;
        private readonly GameObject _playerInventory;


        public PlayerInventory(IUIState gameScreen, MoorestechInputSettings inputSettings, GameObject playerInventory)
        {
            _gameScreen = gameScreen;
            _inputSettings = inputSettings;
            _playerInventory = playerInventory;
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered;
        }

        public IUIState GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered)
            {
                return _gameScreen;
            }

            return this;
        }

        public void OnEnter() { _playerInventory.SetActive(true); }

        public void OnExit() { _playerInventory.SetActive(false); }
    }
}