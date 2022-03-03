using System.Collections.Generic;
using MainGame.Control.UI.UIState.UIState;

namespace MainGame.Control.UI.UIState
{
    public class UIStateDictionary
    {
        private Dictionary<UIStateEnum,IUIState> _stateDictionary = new();

        public UIStateDictionary(GameScreenState gameScreenState,PlayerInventoryState playerInventoryState,BlockInventoryState blockInventoryState,PauseMenuState pauseMenuState)
        {
            _stateDictionary.Add(UIStateEnum.GameScreen,gameScreenState);
            _stateDictionary.Add(UIStateEnum.PlayerInventory,playerInventoryState);
            _stateDictionary.Add(UIStateEnum.BlockInventory,blockInventoryState);
            _stateDictionary.Add(UIStateEnum.PauseMenu,pauseMenuState);
        }

        public IUIState GetState(UIStateEnum state)
        {
            return _stateDictionary[state];
        }
    }
}