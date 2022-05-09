using MainGame.UnityView.UI.CraftRecipe;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class RecipeViewState : IUIState
    {
        private readonly MoorestechInputSettings _inputSettings;
        private readonly RecipeViewerObject _recipeViewerObject;
        private readonly ItemListViewer _itemListViewer;
        

        private UIStateEnum _lastInventoryUI;

        public RecipeViewState(MoorestechInputSettings inputSettings, RecipeViewerObject recipeViewerObject,ItemListViewer itemListViewer)
        {
            _itemListViewer = itemListViewer;
            _inputSettings = inputSettings;
            _recipeViewerObject = recipeViewerObject;
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (_inputSettings.UI.OpenInventory.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            if (_inputSettings.UI.CloseUI.triggered)
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
            _itemListViewer.gameObject.SetActive(true);
            _recipeViewerObject.gameObject.SetActive(true);
        }

        public void OnExit()
        {
            _itemListViewer.gameObject.SetActive(false);
            _recipeViewerObject.gameObject.SetActive(false);
        }
    }
}