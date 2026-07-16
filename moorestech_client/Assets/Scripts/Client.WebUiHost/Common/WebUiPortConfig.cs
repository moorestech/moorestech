namespace Client.WebUiHost.Common
{
    /// <summary>
    /// Web UI のベースポート定数と、起動時に確定した実ポートの保持
    /// Base port constants for the Web UI and the actual ports resolved at startup
    /// </summary>
    public static class WebUiPortConfig
    {
        // ベース値は ephemeral レンジ（Linux 32768〜 / macOS・Win 49152〜）より下の非常用帯から選定
        // Base values sit below every OS ephemeral range (Linux 32768+, macOS/Win 49152+) in an uncommon band
        public const int KestrelBasePort = 25050;
        public const int ViteBasePort = 25173;

        // ベースから何ポートまでインクリメント探索するか
        // How many ports to probe upward from the base
        public const int PortSearchRange = 20;

        // 起動時に確定した Vite の実ポート。0 は未確定（CORS 検査は全拒否になる）
        // Actual Vite port resolved at startup; 0 means unresolved (CORS check rejects everything)
        public static int VitePort => _vitePort;
        private static int _vitePort;

        public static void SetVitePort(int port)
        {
            _vitePort = port;
        }
    }
}
