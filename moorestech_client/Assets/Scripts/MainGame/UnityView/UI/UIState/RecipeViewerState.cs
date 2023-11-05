using MainGame.UnityView.Control;
using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class RecipeViewState : IUIState
    {
        private readonly CraftRecipeItemListViewer _craftRecipeItemListViewer;
        private readonly RecipeViewerObject _recipeViewerObject;

        private bool _isRecipePlaceButton;

        private UIStateEnum _lastInventoryUI;

        public RecipeViewState(RecipeViewerObject recipeViewerObject, CraftRecipeItemListViewer craftRecipeItemListViewer, RecipePlaceButton recipePlaceButton)
        {
            _craftRecipeItemListViewer = craftRecipeItemListViewer;
            _recipeViewerObject = recipeViewerObject;
            recipePlaceButton.OnClick += _ => { _isRecipePlaceButton = true; };
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return _lastInventoryUI;

            if (_isRecipePlaceButton) return UIStateEnum.PlayerInventory;

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _isRecipePlaceButton = false;
            //連続してレシピをクリックされたとき(前回もRecipeViewerだった時)は_lastInventoryUIにデータを入れない
            if (lastStateEnum != UIStateEnum.RecipeViewer) _lastInventoryUI = lastStateEnum;
            _craftRecipeItemListViewer.gameObject.SetActive(true);
            _recipeViewerObject.gameObject.SetActive(true);
        }

        public void OnExit()
        {
            _craftRecipeItemListViewer.gameObject.SetActive(false);
            _recipeViewerObject.gameObject.SetActive(false);
        }
    }
}