using System.Collections.Generic;

namespace Client.Game.InGame.UI.UIState.State.NestedPause
{
    // 入れ子サブステートのIF。IUIStateの簡易版
    // Sub-state interface for nested screens. A simplified counterpart of IUIState
    public interface INestedPauseSubState
    {
        void OnEnter();
        
        // 別のサブステートへ遷移する場合は遷移先を返す。nullなら継続
        // Return the next sub-state to transit to, or null to stay in the current one
        NestedPauseSubStateEnum? GetNextUpdate();
        
        void OnExit();
        
        // このサブステートの操作ヒント。遷移判定と同じ場所で宣言する（ADR-0032）
        // This sub-state's key hints, declared beside its transition checks (ADR-0032)
        IReadOnlyList<KeyHint> GetKeyHints();
        
        // 滞在中に自機の移動を止めるか。入れ子ポーズを持つ画面はここが正
        // Whether player movement stops while this sub-state shows; the authority for screens that own a nested pause
        bool LocksPlayerMovement();
    }
}
