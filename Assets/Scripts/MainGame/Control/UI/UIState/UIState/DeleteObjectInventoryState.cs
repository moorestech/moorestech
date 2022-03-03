using MainGame.Control.UI.UIState.UIObject;

namespace MainGame.Control.UI.UIState.UIState
{
    public class DeleteObjectInventoryState : IUIState
    {
        private readonly MoorestechInputSettings _inputSettings;
        private readonly DeleteBarObject _deleteBarObject;

        public DeleteObjectInventoryState(MoorestechInputSettings inputSettings, DeleteBarObject deleteBarObject)
        {
            _inputSettings = inputSettings;
            _deleteBarObject = deleteBarObject;
            deleteBarObject.gameObject.SetActive(false);
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            return UIStateEnum.PauseMenu;
        }

        public void OnEnter()
        {
            _deleteBarObject.gameObject.SetActive(true);
        }

        public void OnExit()
        {
            _deleteBarObject.gameObject.SetActive(false);
        }

    }
}