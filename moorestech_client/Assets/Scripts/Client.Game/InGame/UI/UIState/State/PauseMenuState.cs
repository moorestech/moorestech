using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State
{
    public class PauseMenuState : IUIState
    {
        private readonly PauseMenuObject _pauseMenu;
        
        public PauseMenuState(PauseMenuObject pauseMenu)
        {
            _pauseMenu = pauseMenu;
            pauseMenu.gameObject.SetActive(false);
        }
        
        public UITransitContext GetNextUpdate()
        {
            if (InputManager.UI.CloseUI.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);

            return null;
        }

        public void OnEnter(UITransitContext context)
        {
            _pauseMenu.gameObject.SetActive(true);
            InputManager.MouseCursorVisible(true);
            KeyControlDescription.Instance.SetText("Esc: ゲームに戻る");
        }
        
        public void OnExit()
        {
            _pauseMenu.gameObject.SetActive(false);
        }
    }
}