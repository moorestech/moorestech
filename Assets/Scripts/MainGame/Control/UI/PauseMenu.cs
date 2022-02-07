namespace MainGame.Control.UI
{
    public class PauseMenu : IUIState
    {
        private IUIState _gameScreen;

        public PauseMenu(IUIState gameScreen)
        {
            _gameScreen = gameScreen;
        }

        public bool IsNext()
        {
            throw new System.NotImplementedException();
        }

        public IUIState GetNext()
        {
            throw new System.NotImplementedException();
        }

        public void OnEnter()
        {
            throw new System.NotImplementedException();
        }

        public void OnExit()
        {
            throw new System.NotImplementedException();
        }
    }
}