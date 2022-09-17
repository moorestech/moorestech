using System;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class PlayerInventoryState : IUIState
    {
        private readonly PlayerInventoryObject _playerInventory;
        
        private readonly CraftRecipeItemListViewer _craftRecipeItemListViewer;
        private readonly ItemRecipePresenter _itemRecipePresenter;


        public event Action OnOpenInventory;

        public PlayerInventoryState(PlayerInventoryObject playerInventory,
            CraftRecipeItemListViewer craftRecipeItemListViewer,ItemRecipePresenter itemRecipePresenter)
        {
            _playerInventory = playerInventory;
            _craftRecipeItemListViewer = craftRecipeItemListViewer;
            _itemRecipePresenter = itemRecipePresenter;

            playerInventory.gameObject.SetActive(false);
        }

        public bool IsNext()
        {
            return InputManager.Settings.UI.CloseUI.triggered || InputManager.Settings.UI.OpenInventory.triggered || _itemRecipePresenter.IsClicked;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.Settings.UI.CloseUI.triggered || InputManager.Settings.UI.OpenInventory.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            if (_itemRecipePresenter.IsClicked)
            {
                return UIStateEnum.RecipeViewer;
            }

            return UIStateEnum.PlayerInventory;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _playerInventory.gameObject.SetActive(true);
            _playerInventory.SetCraftInventory();
            _craftRecipeItemListViewer.gameObject.SetActive(true);
            
            OnOpenInventory?.Invoke();
        }

        public void OnExit()
        {
            _playerInventory.gameObject.SetActive(false);
            _craftRecipeItemListViewer.gameObject.SetActive(false);
        }
    }
}