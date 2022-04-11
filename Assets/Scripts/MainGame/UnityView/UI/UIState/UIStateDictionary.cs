using System.Collections.Generic;
using MainGame.Control.UI.UIState.UIState;

namespace MainGame.UnityView.UI.UIState
{
    public class UIStateDictionary
    {
        private Dictionary<UIStateEnum,IUIState> _stateDictionary = new();

        public UIStateDictionary(GameScreenState gameScreenState,PlayerInventoryState playerInventoryState,BlockInventoryState blockInventoryState,PauseMenuState pauseMenuState,DeleteObjectInventoryState deleteObjectInventoryState,BlockPlaceState blockPlaceState,RecipeViewState recipeViewState)
        {
            _stateDictionary.Add(UIStateEnum.GameScreen,gameScreenState);
            _stateDictionary.Add(UIStateEnum.PlayerInventory,playerInventoryState);
            _stateDictionary.Add(UIStateEnum.BlockInventory,blockInventoryState);
            _stateDictionary.Add(UIStateEnum.PauseMenu,pauseMenuState);
            _stateDictionary.Add(UIStateEnum.DeleteBar,deleteObjectInventoryState);
            _stateDictionary.Add(UIStateEnum.BlockPlace,blockPlaceState);
            _stateDictionary.Add(UIStateEnum.RecipeViewer,recipeViewState);
        }

        public IUIState GetState(UIStateEnum state)
        {
            return _stateDictionary[state];
        }
    }
}