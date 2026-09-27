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
        /// 滞在中に自機の移動（WASD・ジャンプ・ダッシュ）を止めるか。メニュー画面がtrue
        /// trueはWeb UIの背景ディム画面族（uiScreenRouting.ts の backdrop）に、WASDを画面自身が奪う画面を足した集合
        /// 列車HUDはbackdropを出さないがWASDが列車操作なのでtrue。背景ディムの有無だけで決めてはいけない
        /// 入れ子ポーズを持つ画面は定数ではなく表示中のサブステートの宣言を返すため、滞在中に値が変わる
        /// Whether player movement (WASD, jump, sprint) stops while this screen is open; true for menu screens
        /// True is the Web UI's dimmed-backdrop screens (backdrop in uiScreenRouting.ts) plus screens that take WASD themselves
        /// The train HUD shows no backdrop yet is true because WASD drives the train; the backdrop alone must not decide this
        /// Screens owning a nested pause return their showing sub-state's declaration, so the value changes mid-screen
        /// </summary>
        public bool LocksPlayerMovement();

        /// <summary>
        /// この画面の操作ヒント。遷移判定と同じ場所で宣言し、ずれを構造的に防ぐ（ADR-0032）
        /// This screen's key hints, declared beside the transition checks so they cannot drift (ADR-0032)
        /// </summary>
        public IReadOnlyList<KeyHint> GetKeyHints();
    }
}
