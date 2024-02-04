using MainGame.UnityView.Control;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class DeleteObjectInventoryState : IUIState
    {
        private readonly DeleteBarObject _deleteBarObject;
        
        public DeleteObjectInventoryState(DeleteBarObject deleteBarObject)
        {
            _deleteBarObject = deleteBarObject;
            deleteBarObject.gameObject.SetActive(false);
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.GameScreen;

            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;


            return UIStateEnum.Current;
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