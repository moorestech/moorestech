using System;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class PlayerInventoryState : IUIState
    {
        private readonly CraftInventoryObjectCreator _craftInventory;

        private readonly CraftRecipeItemListViewer _craftRecipeItemListViewer;
        private readonly ItemRecipePresenter _itemRecipePresenter;

        public PlayerInventoryState(CraftInventoryObjectCreator craftInventory,
            CraftRecipeItemListViewer craftRecipeItemListViewer, ItemRecipePresenter itemRecipePresenter)
        {
            _craftInventory = craftInventory;
            _craftRecipeItemListViewer = craftRecipeItemListViewer;
            _itemRecipePresenter = itemRecipePresenter;

            craftInventory.gameObject.SetActive(false);
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;

            if (_itemRecipePresenter.IsClicked) return UIStateEnum.RecipeViewer;

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _craftInventory.gameObject.SetActive(true);
            _craftInventory.SetCraftInventory();
            _craftRecipeItemListViewer.gameObject.SetActive(true);

            OnOpenInventory?.Invoke();
        }

        public void OnExit()
        {
            _craftInventory.gameObject.SetActive(false);
            _craftRecipeItemListViewer.gameObject.SetActive(false);
        }


        public event Action OnOpenInventory;
    }
}