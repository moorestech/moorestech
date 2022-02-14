using MainGame.Network.Send;
using UnityEngine;

namespace MainGame.Control.UI.UIState.UIState
{
    public class PlayerInventoryState : IUIState
    {
        private readonly IUIState _gameScreen;
        private readonly MoorestechInputSettings _inputSettings;
        private readonly GameObject _playerInventory;
        private readonly RequestPlayerInventoryProtocol _requestPlayerInventoryProtocol;


        public PlayerInventoryState(IUIState gameScreen, MoorestechInputSettings inputSettings, GameObject playerInventory,
            RequestPlayerInventoryProtocol requestPlayerInventoryProtocol)
        {
            _gameScreen = gameScreen;
            _inputSettings = inputSettings;
            _playerInventory = playerInventory;
            _requestPlayerInventoryProtocol = requestPlayerInventoryProtocol;
            
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

        public void OnEnter()
        {
            _playerInventory.SetActive(true);
            _requestPlayerInventoryProtocol.Send();
        }

        public void OnExit() { _playerInventory.SetActive(false); }
    }
}