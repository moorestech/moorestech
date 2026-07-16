using System.Text.RegularExpressions;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// Vite dev server の stdout から実ポートをパースする純関数
    /// Pure parser that extracts the actual port from Vite dev server stdout
    /// </summary>
    public static class ViteOutputParser
    {
        // ANSI エスケープ（色・装飾）を除去してから Local 行の URL 末尾ポートを取る
        // Strip ANSI escapes (colors/styles) first, then capture the trailing port of the Local line URL
        private static readonly Regex AnsiEscapeRegex = new(@"\x1b\[[0-9;]*m");
        private static readonly Regex LocalPortRegex = new(@"Local:\s+https?://[^:/\s]+:(\d+)");

        public static bool TryParseLocalPort(string line, out int port)
        {
            port = 0;
            if (string.IsNullOrEmpty(line)) return false;

            var plain = AnsiEscapeRegex.Replace(line, "");
            var match = LocalPortRegex.Match(plain);
            if (!match.Success) return false;

            return int.TryParse(match.Groups[1].Value, out port) && port > 0;
        }
    }
}
