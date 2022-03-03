using MainGame.Control.UI.UIState.UIObject;
using MainGame.Network.Send;
using UnityEngine;

namespace MainGame.Control.UI.UIState.UIState
{
    public class PlayerInventoryState : IUIState
    {
        private readonly PlayerInventoryObject _playerInventory;
        
        private readonly MoorestechInputSettings _inputSettings;
        private readonly RequestPlayerInventoryProtocol _requestPlayerInventoryProtocol;


        public PlayerInventoryState( MoorestechInputSettings inputSettings, PlayerInventoryObject playerInventory,
            RequestPlayerInventoryProtocol requestPlayerInventoryProtocol)
        {
            _inputSettings = inputSettings;
            _playerInventory = playerInventory;
            _requestPlayerInventoryProtocol = requestPlayerInventoryProtocol;
            //起動時に初回のインベントリを取得
            _requestPlayerInventoryProtocol.Send();
            
            playerInventory.gameObject.SetActive(false);
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            return UIStateEnum.PlayerInventory;
        }

        public void OnEnter()
        {
            _playerInventory.gameObject.SetActive(true);
            _requestPlayerInventoryProtocol.Send();
        }

        public void OnExit() { _playerInventory.gameObject.SetActive(false); }
    }
}