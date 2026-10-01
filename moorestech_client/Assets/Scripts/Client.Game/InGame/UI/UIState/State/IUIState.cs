using System.Collections.Generic;

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

        /// <summary>
        /// 滞在中に自機の移動（WASD・ジャンプ・ダッシュ）を止めるか。trueはポーズメニュー（入れ子のポーズを含む）と、WASDを画面自身が奪う画面（列車HUD）だけ
        /// 入れ子を持つ画面は定数ではなく表示中のサブステートの宣言を返すため、滞在中に値が変わる
        /// Whether player movement (WASD, jump, sprint) stops while this screen is open; true only for the pause menu (including nested pause) and screens that take WASD themselves (train HUD)
        /// Screens owning a nested state return the showing sub-state's declaration, so the value changes mid-screen
        /// </summary>
        public bool LocksPlayerMovement();

        /// <summary>
        /// この画面の操作ヒント。遷移判定と同じ場所で宣言し、ずれを構造的に防ぐ（ADR-0032）
        /// This screen's key hints, declared beside the transition checks so they cannot drift (ADR-0032)
        /// </summary>
        public IReadOnlyList<KeyHint> GetKeyHints();
    }
}
