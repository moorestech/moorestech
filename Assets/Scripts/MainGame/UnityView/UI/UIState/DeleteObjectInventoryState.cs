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
            return InputManager.UI.CloseUI.GetKey || 
                   InputManager.UI.BlockDelete.GetKey || 
                   _selectHotBarControl.IsClicked || InputManager.UI.HotBar.ReadValue<int>() != 0 || 
                   InputManager.UI.OpenInventory.GetKey ||
                   InputManager.UI.OpenMenu.GetKey;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKey || InputManager.UI.BlockDelete.GetKey)
            {
                return UIStateEnum.GameScreen;
            }

            if (_selectHotBarControl.IsClicked || InputManager.UI.HotBar.ReadValue<int>() != 0)
            {
                return UIStateEnum.BlockPlace;
            }
            if (InputManager.UI.OpenInventory.GetKey)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (InputManager.UI.OpenMenu.GetKey)
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