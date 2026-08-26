using Client.Game.Skit;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // スキット再生中のサブステート。Escは会話UI復帰を優先し、表示中ならポーズメニューを開く
    // Sub-state while the skit plays. Esc restores the hidden dialogue UI first; when visible, it opens the pause menu
    public class SkitPlayingSubState : ISkitScreenSubState
    {
        private readonly SkitManager _skitManager;
        
        public SkitPlayingSubState(SkitManager skitManager)
        {
            _skitManager = skitManager;
        }
        
        public void OnEnter()
        {
            // スキット中はカーソルを表示してUIを操作できるようにする
            // Keep the cursor visible during the skit so the UI stays operable
            InputManager.MouseCursorVisible(true);
        }
        
        public SkitScreenUIStateEnum? GetNextUpdate()
        {
            if (!InputManager.UI.OpenMenu.GetKeyDown) return null;
            
            // webモードのポーズメニューはSkitPresentationStateStoreのブロック対象なので開かない（ADR 0035）
            // The web-mode pause menu is blocked by SkitPresentationStateStore, so do not open it (ADR 0035)
            if (WebUiScreenGate.IsWebUiMode) return null;
            
            // 会話UIが隠れているなら復帰のみ。メニューは次のEscで開く
            // If the dialogue UI is hidden, only restore it; the next Esc opens the menu
            if (_skitManager.IsSkitUiHidden)
            {
                _skitManager.ShowHiddenSkitUi();
                return null;
            }
            
            return SkitScreenUIStateEnum.PauseMenu;
        }
        
        public void OnExit()
        {
        }
    }
}
