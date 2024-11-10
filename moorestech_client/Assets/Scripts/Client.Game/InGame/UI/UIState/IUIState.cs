namespace Client.Game.InGame.UI.UIState
{
    public interface IUIState
    {
        public void OnEnter(UIStateEnum lastStateEnum);
        public UIStateEnum GetNextUpdate();
        public void OnExit();
    }
}