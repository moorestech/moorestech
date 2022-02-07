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
            playerInventory.SetActive(false);
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

        public void OnEnter() { _playerInventory.SetActive(true); }

        public void OnExit() { _playerInventory.SetActive(false); }
    }
}