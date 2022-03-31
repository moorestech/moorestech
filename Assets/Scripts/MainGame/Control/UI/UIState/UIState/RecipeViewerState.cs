using MainGame.Control.UI.UIState.UIObject;

namespace MainGame.Control.UI.UIState.UIState
{
    public class RecipeViewState : IUIState
    {
        private readonly MoorestechInputSettings _inputSettings;
        private readonly RecipeViewObject _recipeViewObject;
        

        private UIStateEnum _lastUI;

        public RecipeViewState(MoorestechInputSettings inputSettings, RecipeViewObject recipeViewObject)
        {
            _inputSettings = inputSettings;
            _recipeViewObject = recipeViewObject;
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
        }

        public void OnExit()
        {
            
        }
    }
}