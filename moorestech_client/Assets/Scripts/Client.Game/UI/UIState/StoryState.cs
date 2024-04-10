namespace Client.Game.UI.UIState
{
    public class StoryState : IUIState
    {
        public UIStateEnum GetNext()
        {
            return UIStateEnum.Current;
        }
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            throw new System.NotImplementedException();
        }
        public void OnExit()
        {
            throw new System.NotImplementedException();
        }
    }
}