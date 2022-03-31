using MainGame.Control.UI.UIState.UIObject;
using MainGame.UnityView.UI.CraftRecipe;

namespace MainGame.Control.UI.UIState.UIState
{
    public class RecipeViewState : IUIState
    {
        private readonly MoorestechInputSettings _inputSettings;
        private readonly RecipeViewerObject _recipeViewerObject;
        private readonly ItemListViewer _itemListViewer;
        

        private UIStateEnum _lastUI;

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
                return _lastUI;
            }

            return UIStateEnum.RecipeViewer;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _lastUI = lastStateEnum;
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