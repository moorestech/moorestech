using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;

namespace Client.Game.InGame.UI.UIState
{
    public class PauseMenuState : IUIState
    {
        private readonly PauseMenuObject _pauseMenu;
        
        public PauseMenuState(PauseMenuObject pauseMenu)
        {
            _pauseMenu = pauseMenu;
            pauseMenu.gameObject.SetActive(false);
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (InputManager.UI.CloseUI.GetKeyDown) return UIStateEnum.GameScreen;
            
            return UIStateEnum.Current;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _pauseMenu.gameObject.SetActive(true);
            InputManager.MouseCursorVisible(true);
        }
        
        public void OnExit()
        {
            _pauseMenu.gameObject.SetActive(false);
        }
    }
}