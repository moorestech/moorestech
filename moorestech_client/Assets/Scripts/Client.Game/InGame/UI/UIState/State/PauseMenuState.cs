using Client.Game.InGame.UI.UIState.State.PauseMenu;

namespace Client.Game.InGame.UI.UIState.State
{
    public class PauseMenuState : IUIState
    {
        private readonly PauseMenuStateService _pauseMenuStateService;
        
        public PauseMenuState(PauseMenuStateService pauseMenu)
        {
            _pauseMenuStateService = pauseMenu;
        }
        
        public UITransitContext GetNextUpdate()
        {
            return _pauseMenuStateService.GetNextUpdate();
        }

        public void OnEnter(UITransitContext context)
        {
            _pauseMenuStateService.OnEnter();
        }
        
        public void OnExit()
        {
            _pauseMenuStateService.OnExit();
        }
    }
}