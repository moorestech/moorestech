namespace Client.Game.InGame.UI.UIState
{
    public interface IUIState
    {
        public void OnEnter(UIStateEnum lastStateEnum);
        public UIStateEnum GetNextUpdate();
        public void OnExit();
    }
    
    public struct UITransitContext
    {
        public UIStateEnum LastStateEnum;
        
        public T GetContext<T>()
        {
            // TODO コンテキストコンテナを作る必要がある
        }
    }
}