using UnityEngine;

namespace MainGame.Control.UI.Control.UIState
{
    public class PauseMenu : IUIState
    {
        private IUIState _gameScreen;
        private readonly MoorestechInputSettings _inputSettings;
        private readonly GameObject _pauseMenu;

        public PauseMenu(IUIState gameScreen,MoorestechInputSettings inputSettings,GameObject pauseMenu)
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