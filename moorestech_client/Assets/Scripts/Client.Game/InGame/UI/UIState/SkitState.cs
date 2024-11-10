using Client.Game.Skit;

namespace Client.Game.InGame.UI.UIState
{
    public class SkitState : IUIState
    {
        private readonly SkitManager _skitManager;
        
        public SkitState(SkitManager skitManager)
        {
            _skitManager = skitManager;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Current;
            return UIStateEnum.GameScreen;
        }
        
        public void OnExit()
        {
        }
    }
}