using System;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class PlayerInventoryState : IUIState
    {
        private readonly CraftInventoryObjectCreator _craftInventory;


        public PlayerInventoryState(CraftInventoryObjectCreator craftInventory)
        {
            _craftInventory = craftInventory;

            craftInventory.gameObject.SetActive(false);
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _craftInventory.gameObject.SetActive(true);
            _craftInventory.SetCraftInventory();

            OnOpenInventory?.Invoke();
        }

        public void OnExit()
        {
            _craftInventory.gameObject.SetActive(false);
        }


        public event Action OnOpenInventory;
    }
}