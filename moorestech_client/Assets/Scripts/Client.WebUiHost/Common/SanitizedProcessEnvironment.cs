using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Common
{
    /// <summary>
    /// 子プロセスへ渡す環境変数から不正UTF-8由来のエントリだけを除外する
    /// Drops only the env entries corrupted by invalid UTF-8 before they reach child processes
    /// エージェントハーネス等が注入した環境変数（例: HERMES_SESSION_CHAT_NAME）がマルチバイト境界で
    /// 切り詰められると不正なバイト列になり、node等の子プロセスがenvパースで死ぬことがある。
    /// 正常な変数はすべて素通しし、除外時は必ず変数名を警告ログに出す。
    /// Env vars injected by agent harnesses (e.g. HERMES_SESSION_CHAT_NAME) can carry invalid byte
    /// sequences when truncated mid multi-byte character, which can kill child processes such as node
    /// during env parsing. Every healthy var passes through untouched; each drop is logged by name.
    /// </summary>
    public static class SanitizedProcessEnvironment
    {
        // CEFヘルパー等のネイティブspawnはProcessStartInfoを通らないため、Unity自身のenvも起動時に浄化する
        // Native spawns such as the CEF helper bypass ProcessStartInfo, so Unity's own env is scrubbed at startup
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void SanitizeCurrentProcess()
        {
            var corruptedNames = new List<string>();
            foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                if (ContainsUndecodableMarker((string)entry.Key) || ContainsUndecodableMarker((string)entry.Value))
                {
                    corruptedNames.Add((string)entry.Key);
                }
            }

            foreach (var name in corruptedNames)
            {
                // unsetenv相当でネイティブenvironからも除去される（名前自体が不正な場合のみ実バイト列と一致せず残る）
                // Removed from the native environ via unsetenv (only a corrupt name itself cannot match the raw bytes)
                System.Environment.SetEnvironmentVariable(name, null);
                Debug.LogWarning($"[SanitizedProcessEnvironment] 不正なUTF-8バイト列を含む環境変数を自プロセスから除去しました: '{EscapeForLog(name)}' / Scrubbed env var containing invalid UTF-8 bytes from this process");
            }
        }

        public static void Sanitize(ProcessStartInfo startInfo)
        {
            // 破損エントリを列挙してから削除する（走査中の辞書変更を避ける）
            // Collect corrupted entries first, then remove them (no mutation while iterating)
            var corruptedNames = new List<string>();
            foreach (var pair in startInfo.Environment)
            {
                if (ContainsUndecodableMarker(pair.Key) || ContainsUndecodableMarker(pair.Value))
                {
                    corruptedNames.Add(pair.Key);
                }
            }

            foreach (var name in corruptedNames)
            {
                startInfo.Environment.Remove(name);
                Debug.LogWarning($"[SanitizedProcessEnvironment] 不正なUTF-8バイト列を含む環境変数を子プロセスへ渡しません: '{EscapeForLog(name)}' / Dropped env var containing invalid UTF-8 bytes");
            }
        }

        // デコード不能バイトの痕跡（置換文字U+FFFD・孤立サロゲート）を検出する
        // Detects undecodable-byte markers: replacement char U+FFFD and lone surrogates
        private static bool ContainsUndecodableMarker(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];
                if (character == '\uFFFD') return true;

                // 上位サロゲートは直後に下位サロゲートが無ければ孤立
                // A high surrogate is lone unless immediately followed by a low surrogate
                if (char.IsHighSurrogate(character))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1])) return true;
                    i++;
                    continue;
                }

                if (char.IsLowSurrogate(character)) return true;
            }

            return false;
        }

        // ログ自体が壊れないよう、不正文字をコードポイント表記へ置換して出力する
        // Escape corrupt characters into code-point notation so the log line itself stays valid
        private static string EscapeForLog(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                if (character == '\uFFFD' || char.IsSurrogate(character))
                {
                    builder.Append($"<U+{(int)character:X4}>");
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}
