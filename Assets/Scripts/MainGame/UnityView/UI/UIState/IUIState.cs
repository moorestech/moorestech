namespace MainGame.UnityView.UI.UIState
{
    public interface IUIState
    {
        public UIStateEnum GetNext();
        public void OnEnter(UIStateEnum lastStateEnum);
        public void OnExit();
    }
}