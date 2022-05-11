using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class PauseMenuState : IUIState
    {
        private readonly MoorestechInputSettings _inputSettings;
        private readonly PauseMenuObject _pauseMenu;

        public PauseMenuState(MoorestechInputSettings inputSettings,PauseMenuObject pauseMenu)
        {
            _inputSettings = inputSettings;
            _pauseMenu = pauseMenu;
            pauseMenu.gameObject.SetActive(false);
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            return UIStateEnum.PauseMenu;
        }

        public void OnEnter(UIStateEnum lastStateEnum) { _pauseMenu.gameObject.SetActive(true); }

        public void OnExit() { _pauseMenu.gameObject.SetActive(false); }
    }
}