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
        /// この画面の操作ヒント。遷移判定と同じ場所で宣言し、ずれを構造的に防ぐ（ADR-0032）
        /// This screen's key hints, declared beside the transition checks so they cannot drift (ADR-0032)
        /// </summary>
        public IReadOnlyList<KeyHint> GetKeyHints();
    }
}
