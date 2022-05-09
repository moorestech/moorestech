using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class DeleteObjectInventoryState : IUIState
    {
        private readonly MoorestechInputSettings _input;
        private readonly DeleteBarObject _deleteBarObject;
        private readonly SelectHotBarControl _selectHotBarControl;

        public DeleteObjectInventoryState(MoorestechInputSettings input, DeleteBarObject deleteBarObject,SelectHotBarControl selectHotBarControl)
        {
            _selectHotBarControl = selectHotBarControl;
            _input = input;
            _deleteBarObject = deleteBarObject;
            deleteBarObject.gameObject.SetActive(false);
        }

        public bool IsNext()
        {
            return _input.UI.CloseUI.triggered || 
                   _input.UI.BlockDelete.triggered || 
                   _selectHotBarControl.IsClicked || _input.UI.HotBar.ReadValue<int>() != 0 || 
                   _input.UI.OpenInventory.triggered ||
                   _input.UI.OpenMenu.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (_input.UI.CloseUI.triggered || _input.UI.BlockDelete.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            if (_selectHotBarControl.IsClicked || _input.UI.HotBar.ReadValue<int>() != 0)
            {
                return UIStateEnum.BlockPlace;
            }
            if (_input.UI.OpenInventory.triggered)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (_input.UI.OpenMenu.triggered)
            {
                return UIStateEnum.PauseMenu;
            }
            

            return UIStateEnum.DeleteBar;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _deleteBarObject.gameObject.SetActive(true);
        }

        public void OnExit()
        {
            _deleteBarObject.gameObject.SetActive(false);
        }

    }
}