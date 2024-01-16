using System.Collections.Generic;

namespace MainGame.UnityView.UI.UIState
{
    public class UIStateDictionary
    {
        private readonly Dictionary<UIStateEnum, IUIState> _stateDictionary = new();

        public UIStateDictionary(GameScreenState gameScreenState, PlayerInventoryState playerInventoryState, BlockInventoryState blockInventoryState, PauseMenuState pauseMenuState, DeleteObjectInventoryState deleteObjectInventoryState, SelectHotBarState selectHotBarState)
        {
            _stateDictionary.Add(UIStateEnum.GameScreen, gameScreenState);
            _stateDictionary.Add(UIStateEnum.PlayerInventory, playerInventoryState);
            _stateDictionary.Add(UIStateEnum.BlockInventory, blockInventoryState);
            _stateDictionary.Add(UIStateEnum.PauseMenu, pauseMenuState);
            _stateDictionary.Add(UIStateEnum.DeleteBar, deleteObjectInventoryState);
            _stateDictionary.Add(UIStateEnum.SelectHotBar, selectHotBarState);
        }

        public IUIState GetState(UIStateEnum state)
        {
            return _stateDictionary[state];
        }
    }
}