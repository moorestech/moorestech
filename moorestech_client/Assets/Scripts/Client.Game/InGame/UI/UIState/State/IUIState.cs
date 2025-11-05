namespace Client.Game.InGame.UI.UIState.State
{
    public interface IUIState
    {
        public void OnEnter(UITransitContext context);
        
        /// <summary>
        /// 別の状態へ遷移する場合、UITransitContextを返す。nullを返した場合、状態は継続される。
        /// If transitioning to another state, return a UITransitContext. If null is returned, the state continues.
        /// </summary>
        public UITransitContext GetNextUpdate();
        
        public void OnExit();
    }
}