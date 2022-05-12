using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class RecipeViewState : IUIState
    {
        private readonly MoorestechInputSettings _inputSettings;
        private readonly RecipeViewerObject _recipeViewerObject;
        private readonly CraftRecipeItemListViewer _craftRecipeItemListViewer;
        

        private UIStateEnum _lastInventoryUI;

        public RecipeViewState(MoorestechInputSettings inputSettings, RecipeViewerObject recipeViewerObject,CraftRecipeItemListViewer craftRecipeItemListViewer)
        {
            _craftRecipeItemListViewer = craftRecipeItemListViewer;
            _inputSettings = inputSettings;
            _recipeViewerObject = recipeViewerObject;
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered)
            {
                return _lastInventoryUI;
            }

            return UIStateEnum.RecipeViewer;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            //連続してレシピをクリックされたとき(前回もRecipeViewerだった時)は_lastInventoryUIにデータを入れない
            if (lastStateEnum != UIStateEnum.RecipeViewer)
            {
                _lastInventoryUI = lastStateEnum;
            }
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