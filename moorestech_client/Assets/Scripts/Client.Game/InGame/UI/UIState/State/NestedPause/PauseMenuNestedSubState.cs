using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.PauseMenu;

namespace Client.Game.InGame.UI.UIState.State.NestedPause
{
    // ポーズメニュー表示中のサブステート。列車HUDとスキットで共有する
    // Sub-state while the pause menu shows; shared by the train HUD and the skit
    public class PauseMenuNestedSubState : INestedPauseSubState
    {
        private readonly PauseMenuStateService _pauseMenuStateService;
        
        public PauseMenuNestedSubState(PauseMenuStateService pauseMenuStateService)
        {
            _pauseMenuStateService = pauseMenuStateService;
        }
        
        public void OnEnter()
        {
            _pauseMenuStateService.OnEnter();
        }
        
        public NestedPauseSubStateEnum? GetNextUpdate()
        {
            return _pauseMenuStateService.IsClosePause() ? NestedPauseSubStateEnum.GameScreen : null;
        }
        
        public void OnExit()
        {
            _pauseMenuStateService.OnExit();
        }
        
        // ESCは全画面で載せないためポーズ中はヒントごと空になる（ADR-0032）
        // ESC is excluded on every screen, so the pause sub-state carries no hints at all (ADR-0032)
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return Array.Empty<KeyHint>();
        }
    }
}
