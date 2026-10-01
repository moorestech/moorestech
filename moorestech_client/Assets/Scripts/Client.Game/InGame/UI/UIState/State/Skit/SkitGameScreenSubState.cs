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

            // 隠れた会話UIがある間のEscは復帰専用に消費する。storeに拒否された回もメニューは開かない
            // While the dialogue UI is hidden, Esc is spent on restoring it; a store refusal does not open the menu either
            if (_skitManager.TryRestoreHiddenSkitUi() != SkitUiRestoreResult.NothingHidden) return null;

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
        
        // スキット再生中の移動は従来どおり許す。会話UIは背景をディムしない
        // Movement stays allowed while the skit plays; the dialogue UI does not dim the background
        public bool LocksPlayerMovement()
        {
            return false;
        }
    }
}
