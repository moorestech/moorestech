using System.Collections.Generic;

namespace Client.Game.InGame.UI.UIState
{
    public class UIStateDictionary
    {
        private readonly Dictionary<UIStateEnum, IUIState> _stateDictionary = new();
        
        public UIStateDictionary(
            GameScreenState gameScreenState,
            PlayerInventoryState playerInventoryState,
            SubInventoryState subInventoryState,
            PauseMenuState pauseMenuState,
            DeleteBlockState deleteBlockState,
            SkitState skitState,
            PlaceBlockState placeBlockState,
            ChallengeListState challengeListState)
        {
            _stateDictionary.Add(UIStateEnum.GameScreen, gameScreenState);
            _stateDictionary.Add(UIStateEnum.PlayerInventory, playerInventoryState);
            _stateDictionary.Add(UIStateEnum.SubInventory, subInventoryState);
            _stateDictionary.Add(UIStateEnum.PauseMenu, pauseMenuState);
            _stateDictionary.Add(UIStateEnum.DeleteBar, deleteBlockState);
            _stateDictionary.Add(UIStateEnum.Story, skitState);
            _stateDictionary.Add(UIStateEnum.PlaceBlock, placeBlockState);
            _stateDictionary.Add(UIStateEnum.ChallengeList, challengeListState);
        }
        
        public IUIState GetState(UIStateEnum state)
        {
            return _stateDictionary[state];
        }
    }
}