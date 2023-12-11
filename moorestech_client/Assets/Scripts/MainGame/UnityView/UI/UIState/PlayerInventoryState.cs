using System;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class PlayerInventoryState : IUIState
    {
        private readonly CraftInventoryObject _craftInventory;
        private readonly PlayerInventoryController _playerInventoryController;
        
        public PlayerInventoryState(CraftInventoryObject craftInventory,PlayerInventoryController playerInventoryController)
        {
            _craftInventory = craftInventory;
            _playerInventoryController = playerInventoryController;
            
            craftInventory.SetActive(false);
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _craftInventory.SetActive(true);
            _playerInventoryController.SetActive(true);
            _playerInventoryController.SetSubInventory(new EmptySubInventory());

            OnOpenInventory?.Invoke();
        }

        public void OnExit()
        {
            _craftInventory.SetActive(false);
            _playerInventoryController.SetActive(false);
        }


        public event Action OnOpenInventory;
    }
}