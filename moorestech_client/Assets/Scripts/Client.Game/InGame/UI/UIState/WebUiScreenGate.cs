namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// Web UI（CEF）モードを共有する静的ゲート。uGUI廃止Phase1によりWebモード恒久化・uGUIフォールバックは廃止した。
    /// 状態遷移は uGUI アセンブリ内の UIStateControl が唯一の正で、本ゲートは置換済みuGUIビューの表示抑止にだけ使う。
    /// 廃止計画は docs/webui/ugui-retirement-plan.md を参照。
    /// Static gate sharing Web UI (CEF) mode; uGUI-retirement Phase1 made web mode permanent and removed the uGUI fallback.
    /// UIStateControl (in the uGUI assembly) remains the sole state authority; this gate only suppresses replaced uGUI views.
    /// See docs/webui/ugui-retirement-plan.md for the retirement plan.
    /// </summary>
    public static class WebUiScreenGate
    {
        // WebUiHost の起動成否。診断用に記録のみ行い、失敗してもuGUIへはフォールバックしない
        // WebUiHost start success; recorded for diagnostics only — a failure no longer falls back to uGUI
        public static bool IsHostAvailable { get; private set; }

        public static bool IsWebUiMode => true;

        public static void SetHostAvailable(bool available)
        {
            IsHostAvailable = available;
        }
    }
}
