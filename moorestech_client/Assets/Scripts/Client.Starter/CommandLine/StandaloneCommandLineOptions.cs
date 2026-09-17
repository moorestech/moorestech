using System.Collections.Generic;

namespace Client.Starter.CommandLine
{
    /// <summary>
    /// 配布ビルドを自動運転する起動引数の共通読み取り（外部入力なので欠落・空値・重複を同じ規則で拒否する）
    /// Shared reading of the launch arguments that drive a standalone build; external input, so missing, empty and duplicate values are refused by one rule
    /// </summary>
    internal static class StandaloneCommandLineOptions
    {
        public static bool HasFlag(IReadOnlyList<string> args, string flag)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] == flag) return true;
            }
            return false;
        }

        public static bool TryReadRequiredOption(IReadOnlyList<string> args, string option, out string value, out string error)
        {
            value = string.Empty;
            var matchCount = 0;
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] != option) continue;

                matchCount++;
                if (i + 1 < args.Count) value = args[i + 1];
            }

            // 値なし・空値・重複指定を同じ外部入力境界で検査する
            // Validate missing, empty, and duplicate values at the same external-input boundary
            if (matchCount == 0)
            {
                error = $"{option} is required";
                return false;
            }
            if (matchCount != 1)
            {
                error = $"{option} must be specified exactly once";
                return false;
            }
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--"))
            {
                error = $"{option} requires a value";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
