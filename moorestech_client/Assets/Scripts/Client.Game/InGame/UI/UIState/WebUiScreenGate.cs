namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// Web UI（CEF）モードの共有ゲート（恒久有効・uGUIフォールバック撤去済み、ADR 0052）。
    /// 状態遷移は uGUI アセンブリ内の UIStateControl が唯一の正で、本ゲートはワールド空間表示物とUI Toolkitの表示抑止に使う。
    /// 廃止計画は docs/webui/ugui-retirement-plan.md を参照。
    /// Static gate sharing Web UI (CEF) mode; the fallback was already removed and web mode is now permanent (ADR 0052).
    /// UIStateControl (in the uGUI assembly) remains the sole state authority; this gate suppresses world-space visuals and UI Toolkit display.
    /// See docs/webui/ugui-retirement-plan.md for the retirement plan.
    /// </summary>
    public static class WebUiScreenGate
    {
        // WebUiHost の起動成否。診断用に記録のみ行い、失敗してもuGUIへはフォールバックしない
        // WebUiHost start success; recorded for diagnostics only — a failure no longer falls back to uGUI
        private static bool IsHostAvailable { get; set; }

        public static bool IsWebUiMode => true;

        public static void SetHostAvailable(bool available)
        {
            IsHostAvailable = available;
        }
    }
}
