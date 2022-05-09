using System;
using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly MoorestechInputSettings _inputSettings;
        private readonly BlockInventoryObject _blockInventory;
        private readonly ItemListViewer _itemListViewer;
        private readonly ItemRecipePresenter _itemRecipePresenter;
        public event Action OnOpenBlockInventory;
        public event Action OnCloseBlockInventory;

        public BlockInventoryState(MoorestechInputSettings inputSettings, BlockInventoryObject blockInventory,
            ItemListViewer itemListViewer,ItemRecipePresenter itemRecipePresenter)
        {
            _itemListViewer = itemListViewer;
            _itemRecipePresenter = itemRecipePresenter;
            _inputSettings = inputSettings;
            _blockInventory = blockInventory;
            blockInventory.gameObject.SetActive(false);
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered || _itemRecipePresenter.IsClicked;
        }

        public UIStateEnum GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            if (_itemRecipePresenter.IsClicked)
            {
                return UIStateEnum.RecipeViewer;
            }

            return UIStateEnum.BlockInventory;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            OnOpenBlockInventory?.Invoke();
            
            _itemListViewer.gameObject.SetActive(true);
            _blockInventory.gameObject.SetActive(true);
        }

        public void OnExit()
        {
            OnCloseBlockInventory?.Invoke();
            
            _blockInventory.gameObject.SetActive(false);
            _itemListViewer.gameObject.SetActive(false);
        }
    }
}