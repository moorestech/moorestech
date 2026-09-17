using System.Collections.Generic;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 配布ビルドの通し検証を起動するコマンドライン引数（外部入力なので欠落・空・重複を拒否する）
    /// Command-line arguments launching the distribution smoke run; external input, so missing/empty/duplicate values are rejected
    /// </summary>
    public sealed class StandalonePlaytestSmokeSettings
    {
        public const string Marker = "--playtestSmoke";
        public const string PhaseOne = "phase1";
        public const string PhaseTwo = "phase2";
        private const string PhaseOption = "--smokePhase";
        private const string ResultDirectoryOption = "--smokeResultDirectory";

        public readonly string Phase;
        public readonly string ResultDirectory;

        private StandalonePlaytestSmokeSettings(string phase, string resultDirectory)
        {
            Phase = phase;
            ResultDirectory = resultDirectory;
        }

        public static bool HasMarker(IReadOnlyList<string> args)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] == Marker) return true;
            }
            return false;
        }

        public static bool TryParse(IReadOnlyList<string> args, out StandalonePlaytestSmokeSettings settings, out string error)
        {
            settings = null;
            if (!HasMarker(args))
            {
                error = $"{Marker} is required";
                return false;
            }
            if (!TryReadRequiredOption(args, PhaseOption, out var phase, out error)) return false;
            if (!TryReadRequiredOption(args, ResultDirectoryOption, out var resultDirectory, out error)) return false;

            // 未知のphaseは「何も検証しない成功」を作るため、明示的に拒否する
            // An unknown phase would fabricate a success that verified nothing, so refuse it outright
            if (phase != PhaseOne && phase != PhaseTwo)
            {
                error = $"{PhaseOption} must be {PhaseOne} or {PhaseTwo}, but was {phase}";
                return false;
            }

            settings = new StandalonePlaytestSmokeSettings(phase, resultDirectory);
            error = string.Empty;
            return true;
        }

        private static bool TryReadRequiredOption(IReadOnlyList<string> args, string option, out string value, out string error)
        {
            value = string.Empty;
            var matchCount = 0;
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] != option) continue;
                matchCount++;
                if (i + 1 < args.Count) value = args[i + 1];
            }

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
