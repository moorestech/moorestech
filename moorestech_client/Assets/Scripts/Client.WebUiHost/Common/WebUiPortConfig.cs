namespace Client.WebUiHost.Common
{
    /// <summary>
    /// Web UIのベースポート定数と実ポート保持
    /// Base port constants for the Web UI and the actual ports resolved at startup
    /// </summary>
    public static class WebUiPortConfig
    {
        // ベース値は ephemeral レンジ（Linux 32768〜 / macOS・Win 49152〜）より下の非常用帯から選定
        // Base values sit below every OS ephemeral range (Linux 32768+, macOS/Win 49152+) in an uncommon band
        public const int KestrelBasePort = 25050;
        public const int ViteBasePort = 25173;

        public const int PortSearchRange = 20;

        // Vite実ポート。0=未確定(CORS全拒否)
        // Actual Vite port resolved at startup; 0 means unresolved (CORS check rejects everything)
        public static int VitePort => _browserPort;
        public static int BrowserPort => _browserPort;
        private static int _browserPort;

        public static void SetVitePort(int port)
        {
            SetBrowserPort(port);
        }

        public static void SetBrowserPort(int port)
        {
            _browserPort = port;
        }
    }
}
