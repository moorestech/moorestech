using UnityEngine;

namespace MainGame.Control.UI.Control.UIState
{
    public class PauseMenuState : IUIState
    {
        private readonly IUIState _gameScreen;
        private readonly MoorestechInputSettings _inputSettings;
        private readonly GameObject _pauseMenu;

        public PauseMenuState(IUIState gameScreen,MoorestechInputSettings inputSettings,GameObject pauseMenu)
        {
            _gameScreen = gameScreen;
            _inputSettings = inputSettings;
            _pauseMenu = pauseMenu;
            pauseMenu.SetActive(false);
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered;
        }

        public IUIState GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered)
            {
                return _gameScreen;
            }

            return this;
        }

        public void OnEnter() { _pauseMenu.SetActive(true); }

        public void OnExit() { _pauseMenu.SetActive(false); }
    }
}