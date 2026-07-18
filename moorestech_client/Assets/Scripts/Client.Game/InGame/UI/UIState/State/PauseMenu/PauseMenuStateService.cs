using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    public class PauseMenuStateService
    {
        private readonly PauseMenuObject _pauseMenu;
        
        public PauseMenuStateService(PauseMenuObject pauseMenu)
        {
            _pauseMenu = pauseMenu;
            pauseMenu.gameObject.SetActive(false);
        }
        
        public bool IsClosePause()
        {
            return InputManager.UI.CloseUI.GetKeyDown;
        }
        
        public void OnEnter()
        {
            _pauseMenu.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            InputManager.MouseCursorVisible(true);
            KeyControlDescription.Instance.SetText("Esc: ゲームに戻る");
        }
        
        public void OnExit()
        {
            _pauseMenu.gameObject.SetActive(false);
        }
    }
}
