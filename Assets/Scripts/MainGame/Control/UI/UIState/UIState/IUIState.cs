namespace MainGame.Control.UI.UIState.UIState
{
    public interface IUIState
    {
        public bool IsNext();
        public IUIState GetNext();
        public void OnEnter();
        public void OnExit();
    }
}