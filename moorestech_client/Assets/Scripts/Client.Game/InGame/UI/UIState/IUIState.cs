namespace Client.Game.InGame.UI.UIState
{
    public interface IUIState
    {
        public void OnEnter(UIStateEnum lastStateEnum);
        public UIStateEnum GetNext();
        public void OnExit();
    }
}