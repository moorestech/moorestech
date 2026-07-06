namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// Web UI の表示状態を一方通行で共有する静的ゲート。
    /// IsWebUiMode は WebUiCefToggle のみ、CurrentUiState は UIStateControl のみが書き込む。
    /// One-way static gate sharing the Web UI display status.
    /// Only WebUiCefToggle writes IsWebUiMode; only UIStateControl writes CurrentUiState.
    /// </summary>
    public static class WebUiScreenGate
    {
        // Ctrl+I マスタスイッチ（webモード）
        // Ctrl+I master switch (web mode)
        public static bool IsWebUiMode { get; private set; }

        // UIStateControl が公開する現在のUIState
        // Current UI state published by UIStateControl
        public static UIStateEnum CurrentUiState { get; private set; } = UIStateEnum.GameScreen;

        // CEF を表示すべきか（webモード かつ Web実装済み画面ステート）
        // Whether CEF should be shown (web mode AND a web-implemented screen state)
        public static bool IsCefVisible => IsWebUiMode && IsWebScreenState(CurrentUiState);

        public static void SetWebUiMode(bool active)
        {
            IsWebUiMode = active;
        }

        public static void SetCurrentUiState(UIStateEnum state)
        {
            CurrentUiState = state;
        }

        // webモード中に遷移を許可するstate（GameScreen + Web実装済み画面）
        // States reachable while in web mode (GameScreen + web-implemented screens)
        public static bool IsWebSupportedState(UIStateEnum state)
        {
            return state == UIStateEnum.GameScreen || IsWebScreenState(state);
        }

        // CEF に描画を任せる画面ステート
        // Screen states whose rendering is delegated to CEF
        private static bool IsWebScreenState(UIStateEnum state)
        {
            return state == UIStateEnum.PlayerInventory || state == UIStateEnum.SubInventory;
        }
    }
}
