using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class DeleteObjectInventoryState : IUIState
    {
        private readonly DeleteBarObject _deleteBarObject;
        private readonly SelectHotBarControl _selectHotBarControl;

        public DeleteObjectInventoryState(DeleteBarObject deleteBarObject,SelectHotBarControl selectHotBarControl)
        {
            _selectHotBarControl = selectHotBarControl;
            _deleteBarObject = deleteBarObject;
            deleteBarObject.gameObject.SetActive(false);
        }

        public bool IsNext()
        {
            return InputManager.Settings.UI.CloseUI.triggered || 
                   InputManager.Settings.UI.BlockDelete.triggered || 
                   _selectHotBarControl.IsClicked || InputManager.Settings.UI.HotBar.ReadValue<int>() != 0 || 
                   InputManager.Settings.UI.OpenInventory.triggered ||
                   InputManager.Settings.UI.OpenMenu.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.Settings.UI.CloseUI.triggered || InputManager.Settings.UI.BlockDelete.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            if (_selectHotBarControl.IsClicked || InputManager.Settings.UI.HotBar.ReadValue<int>() != 0)
            {
                return UIStateEnum.BlockPlace;
            }
            if (InputManager.Settings.UI.OpenInventory.triggered)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (InputManager.Settings.UI.OpenMenu.triggered)
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