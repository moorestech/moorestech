namespace MainGame.Control.UI
{
    public interface IUIState
    {
        public bool IsNext();
        public IUIState GetNext();
        public void OnEnter();
        public void OnExit();
    }
}