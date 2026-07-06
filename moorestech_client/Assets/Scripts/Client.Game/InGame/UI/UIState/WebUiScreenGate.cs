namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// Web UI（CEF）モードかどうかを一方通行で共有する静的ゲート。CEFトグル値とホスト起動成否のANDで実効モードを決める。
    /// 状態遷移は uGUI の UIStateControl が唯一の正で、本ゲートは置換済みビューの表示抑止にだけ使う。
    /// One-way static gate for Web UI (CEF) mode; the effective mode ANDs the CEF toggle with host-start success.
    /// The uGUI UIStateControl remains the sole state authority; this gate only suppresses replaced views.
    /// </summary>
    public static class WebUiScreenGate
    {
        // CEFトグルの生値。ホスト起動成否と AND を取って実効モードを出す
        // Raw CEF toggle value; AND-combined with host-start success to yield the effective mode
        private static bool _cefToggleActive;

        // WebUiHost の起動成否。InitializeScenePipeline が StartAsync の結果を書き込む
        // WebUiHost start success; written by InitializeScenePipeline from the StartAsync result
        public static bool IsHostAvailable { get; private set; }

        // 実効Web UIモード = CEFトグルON かつ ホスト起動成功。ホストが死んでいれば uGUI へフォールバックする
        // Effective Web UI mode = toggle ON AND host started; falls back to uGUI when the host is dead
        public static bool IsWebUiMode => _cefToggleActive && IsHostAvailable;

        public static void SetWebUiMode(bool active)
        {
            _cefToggleActive = active;
        }

        public static void SetHostAvailable(bool available)
        {
            IsHostAvailable = available;
        }
    }
}
