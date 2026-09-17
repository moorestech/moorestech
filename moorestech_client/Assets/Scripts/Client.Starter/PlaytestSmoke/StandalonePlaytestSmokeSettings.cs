using System;
using System.Collections.Generic;
using Client.Starter.CommandLine;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 配布ビルドの通し検証を起動するコマンドライン引数（外部入力なので欠落・空・重複を拒否する）
    /// Command-line arguments launching the distribution smoke run; external input, so missing/empty/duplicate values are rejected
    /// </summary>
    public sealed class StandalonePlaytestSmokeSettings
    {
        public const string Marker = "--playtestSmoke";
        private const string PhaseOneArgument = "phase1";
        private const string PhaseTwoArgument = "phase2";
        private const string PhaseOption = "--smokePhase";
        private const string ResultDirectoryOption = "--smokeResultDirectory";

        public readonly StandalonePlaytestSmokePhase Phase;
        public readonly string ResultDirectory;

        private StandalonePlaytestSmokeSettings(StandalonePlaytestSmokePhase phase, string resultDirectory)
        {
            Phase = phase;
            ResultDirectory = resultDirectory;
        }

        public static bool HasMarker(IReadOnlyList<string> args)
        {
            return StandaloneCommandLineOptions.HasFlag(args, Marker);
        }

        public static bool TryParse(IReadOnlyList<string> args, out StandalonePlaytestSmokeSettings settings, out string error)
        {
            settings = null;
            if (!HasMarker(args))
            {
                error = $"{Marker} is required";
                return false;
            }
            if (!StandaloneCommandLineOptions.TryReadRequiredOption(args, PhaseOption, out var phaseArgument, out error)) return false;
            if (!StandaloneCommandLineOptions.TryReadRequiredOption(args, ResultDirectoryOption, out var resultDirectory, out error)) return false;

            // 未知のphaseは「何も検証しない成功」を作るため、明示的に拒否する
            // An unknown phase would fabricate a success that verified nothing, so refuse it outright
            StandalonePlaytestSmokePhase phase;
            switch (phaseArgument)
            {
                case PhaseOneArgument: phase = StandalonePlaytestSmokePhase.PhaseOne; break;
                case PhaseTwoArgument: phase = StandalonePlaytestSmokePhase.PhaseTwo; break;
                default:
                    error = $"{PhaseOption} must be {PhaseOneArgument} or {PhaseTwoArgument}, but was {phaseArgument}";
                    return false;
            }

            settings = new StandalonePlaytestSmokeSettings(phase, resultDirectory);
            error = string.Empty;
            return true;
        }

        // result.json とログに出す段階名。回収側は引数と同じ語で読む
        // The phase name written to result.json and logs; the collector reads it with the same word as the argument
        public static string ToArgument(StandalonePlaytestSmokePhase phase)
        {
            switch (phase)
            {
                case StandalonePlaytestSmokePhase.PhaseOne: return PhaseOneArgument;
                case StandalonePlaytestSmokePhase.PhaseTwo: return PhaseTwoArgument;
                default: throw new ArgumentOutOfRangeException(nameof(phase), phase, "unknown smoke phase");
            }
        }
    }
}
