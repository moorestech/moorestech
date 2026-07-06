namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// Web UI（CEF）モードかどうかを一方通行で共有する静的ゲート。書き込みは WebUiCefToggle のみ。
    /// One-way static gate telling whether Web UI (CEF) mode is on; written only by WebUiCefToggle.
    /// 状態遷移は uGUI の UIStateControl が唯一の正で、本ゲートは置換済みビューの表示抑止にだけ使う。
    /// The uGUI UIStateControl remains the sole state authority; this gate only suppresses replaced views.
    /// </summary>
    public static class WebUiScreenGate
    {
        public static bool IsWebUiMode { get; private set; }

        public static void SetWebUiMode(bool active)
        {
            IsWebUiMode = active;
        }
    }
}
