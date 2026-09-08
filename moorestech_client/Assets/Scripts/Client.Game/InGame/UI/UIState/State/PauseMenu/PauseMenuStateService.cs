using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    public class PauseMenuStateService
    {
        public bool IsClosePause()
        {
            return InputManager.UI.CloseUI.GetKeyDown;
        }

        public void OnEnter()
        {
            InputManager.MouseCursorVisible(true);
        }

        public void OnExit()
        {
        }
    }
}
