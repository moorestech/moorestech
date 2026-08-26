using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Game.Skit;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // スキット再生中のサブステート
    // Sub-state while the skit plays
    public class SkitGameScreenSubState : INestedPauseSubState
    {
        private readonly SkitManager _skitManager;
        
        public SkitGameScreenSubState(SkitManager skitManager)
        {
            _skitManager = skitManager;
        }
        
        public void OnEnter()
        {
            // カーソルをUI操作可能に表示
            // Show the cursor for UI operability
            InputManager.MouseCursorVisible(true);
        }
        
        public NestedPauseSubStateEnum? GetNextUpdate()
        {
            if (!InputManager.UI.OpenMenu.GetKeyDown) return null;

            // 会話UIの復帰に成功した回だけメニューを開かない。失敗時はEscを握り潰さずメニューへ進む
            // Only skip opening the menu when restoring the dialogue UI actually succeeds; on failure, fall through instead of swallowing Esc
            if (_skitManager.TryRestoreHiddenSkitUi()) return null;

            return NestedPauseSubStateEnum.PauseMenuScreen;
        }
        
        public void OnExit()
        {
        }
        
        // スキット中の操作はWeb側の会話UIが担うためキーヒントは持たない
        // Skit interaction lives in the web dialogue UI, so this sub-state carries no key hints
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return Array.Empty<KeyHint>();
        }
    }
}
