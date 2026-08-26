using Client.Game.InGame.UI.UIState.State.PauseMenu;

namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // ポーズメニュー表示中のサブステート。Escで閉じてスキット再生へ戻る。背後のスキットは止めない
    // Sub-state while the pause menu shows. Esc closes it and returns to playing. The skit keeps running behind it
    public class SkitPauseMenuSubState : ISkitScreenSubState
    {
        private readonly PauseMenuStateService _pauseMenuStateService;
        
        public SkitPauseMenuSubState(PauseMenuStateService pauseMenuStateService)
        {
            _pauseMenuStateService = pauseMenuStateService;
        }
        
        public void OnEnter()
        {
            _pauseMenuStateService.OnEnter();
        }
        
        public SkitScreenUIStateEnum? GetNextUpdate()
        {
            return _pauseMenuStateService.IsClosePause() ? SkitScreenUIStateEnum.Playing : null;
        }
        
        public void OnExit()
        {
            _pauseMenuStateService.OnExit();
        }
    }
}
