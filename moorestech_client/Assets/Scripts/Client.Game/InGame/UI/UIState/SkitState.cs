using Client.Game.Skit;
using Client.Input;

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
            // スキット中はカーソルを表示してUIを操作できるようにする
            InputManager.MouseCursorVisible(true);
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Current;
            return UIStateEnum.GameScreen;
        }
        
        public void OnExit()
        {
            // スキット終了時はカーソルを非表示に戻す
            InputManager.MouseCursorVisible(false);
        }
    }
}