using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Common
{
    /// <summary>
    /// 子プロセスへ渡す環境変数を組み立てる（不正UTF-8エントリの除外とPATH先頭追加）
    /// Builds the env handed to child processes: drops invalid-UTF-8 entries and prepends PATH directories
    /// エージェントハーネス等が注入した環境変数（例: HERMES_SESSION_CHAT_NAME）がマルチバイト境界で
    /// 切り詰められると不正なバイト列になり、node等の子プロセスがenvパースで死ぬことがある。
    /// 正常な変数はすべて素通しし、除外時は必ず変数名を警告ログに出す。
    /// Env vars injected by agent harnesses (e.g. HERMES_SESSION_CHAT_NAME) can carry invalid byte
    /// sequences when truncated mid multi-byte character, which can kill child processes such as node
    /// during env parsing. Every healthy var passes through untouched; each drop is logged by name.
    /// </summary>
    public static class SanitizedProcessEnvironment
    {
        // 中身が壊れて見えても消してはならない変数（PATHやHOMEを失うとプロセス自体が動かなくなる）
        // Vars that must survive even when their content looks corrupt: losing PATH or HOME breaks the process itself
        private static readonly string[] NeverScrubbedNames =
        {
            "PATH", "HOME", "TMPDIR", "TEMP", "TMP", "USERPROFILE",
            "SystemRoot", "windir", "APPDATA", "LOCALAPPDATA", "COMSPEC", "PATHEXT",
        };

        // CEFヘルパー等のネイティブspawnはProcessStartInfoを通らないため、Unity自身のenvも起動時に浄化する
        // Native spawns such as the CEF helper bypass ProcessStartInfo, so Unity's own env is scrubbed at startup
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void SanitizeCurrentProcess()
        {
            var corruptedNames = new List<string>();
            foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                if (IsCorrupted((string)entry.Key, (string)entry.Value)) corruptedNames.Add((string)entry.Key);
            }

            foreach (var name in corruptedNames)
            {
                // unsetenv相当でネイティブenvironからも除去される（名前自体が不正な場合のみ実バイト列と一致せず残る）
                // Removed from the native environ via unsetenv (only a corrupt name itself cannot match the raw bytes)
                System.Environment.SetEnvironmentVariable(name, null);
                Debug.LogWarning($"[SanitizedProcessEnvironment] 不正なUTF-8バイト列を含む環境変数を自プロセスから除去します: '{EscapeForLog(name)}' / Scrubbing env var containing invalid UTF-8 bytes from this process");
            }

            // 除去できたかは再走査でしか分からない（名前が不正だとunsetenvが実バイト列と一致せず黙って残る）
            // Only a re-scan tells whether the removal worked: a corrupt name silently survives unsetenv
            foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                if (!IsCorrupted((string)entry.Key, (string)entry.Value)) continue;
                Debug.LogError($"[SanitizedProcessEnvironment] 不正なUTF-8バイト列を含む環境変数を除去できず残っています: '{EscapeForLog((string)entry.Key)}' / Failed to scrub env var containing invalid UTF-8 bytes; it survived");
            }
        }

        public static void Sanitize(ProcessStartInfo startInfo)
        {
            // 破損エントリを列挙してから削除する（走査中の辞書変更を避ける）
            // Collect corrupted entries first, then remove them (no mutation while iterating)
            var corruptedNames = new List<string>();
            foreach (var pair in startInfo.Environment)
            {
                if (IsCorrupted(pair.Key, pair.Value)) corruptedNames.Add(pair.Key);
            }

            foreach (var name in corruptedNames)
            {
                startInfo.Environment.Remove(name);
                Debug.LogWarning($"[SanitizedProcessEnvironment] 不正なUTF-8バイト列を含む環境変数を子プロセスへ渡しません: '{EscapeForLog(name)}' / Dropped env var containing invalid UTF-8 bytes");
            }
        }

        public static void PrependPath(ProcessStartInfo startInfo, string directory)
        {
            if (string.IsNullOrEmpty(directory)) return;

            // Windowsでは継承キーが"Path"のため、大小無視で既存キーを特定しないと重複キーになり子に無視される
            // On Windows the inherited key is "Path"; resolve it case-insensitively or a duplicate key gets ignored by children
            var pathKey = "PATH";
            foreach (var key in startInfo.Environment.Keys)
            {
                if (string.Equals(key, "PATH", System.StringComparison.OrdinalIgnoreCase))
                {
                    pathKey = key;
                    break;
                }
            }

            // 浄化済みの辞書側から既存値を取る（親プロセスの生envを読み直すとSanitizeを迂回する）
            // Read the existing value from the sanitized dictionary; re-reading the raw parent env would bypass Sanitize
            startInfo.Environment.TryGetValue(pathKey, out var currentPath);
            startInfo.Environment[pathKey] = string.IsNullOrEmpty(currentPath)
                ? directory
                : $"{directory}{System.IO.Path.PathSeparator}{currentPath}";
        }

        // 除去対象かを判定する（必須変数は壊れて見えても残す・失うと復旧できないため）
        // Decides whether an entry may be dropped; required vars stay even when they look corrupt, since losing them is unrecoverable
        private static bool IsCorrupted(string name, string value)
        {
            foreach (var neverScrubbedName in NeverScrubbedNames)
            {
                if (string.Equals(name, neverScrubbedName, System.StringComparison.OrdinalIgnoreCase)) return false;
            }

            return ContainsUndecodableMarker(name) || ContainsUndecodableMarker(value);
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
                    if (text.Length <= i + 1 || !char.IsLowSurrogate(text[i + 1])) return true;
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
