using System.Collections.Generic;
using Game.MapGeneration.Provisioning;
using Server.Boot;
using Server.Boot.Args;

namespace Client.Starter.StandaloneQa
{
    public sealed class StandaloneTerrainQaSettings
    {
        public const string Marker = "--standaloneTerrainQa";
        private const string ServerDirectoryOption = "--qaServerDirectory";
        private const string WorldDirectoryOption = "--qaWorldDirectory";
        private const string ResultDirectoryOption = "--qaResultDirectory";
        private const string SeedOption = "--qaSeed";

        public readonly string ResultDirectory;
        private readonly string _serverDirectory;
        private readonly string _worldDirectory;
        private readonly int _seed;

        private StandaloneTerrainQaSettings(string serverDirectory, string worldDirectory, string resultDirectory, int seed)
        {
            _serverDirectory = serverDirectory;
            _worldDirectory = worldDirectory;
            ResultDirectory = resultDirectory;
            _seed = seed;
        }

        public static bool HasMarker(IReadOnlyList<string> args)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] == Marker) return true;
            }
            return false;
        }

        public static bool TryParse(IReadOnlyList<string> args, out StandaloneTerrainQaSettings settings, out string error)
        {
            settings = null;
            if (!HasMarker(args))
            {
                error = $"{Marker} is required";
                return false;
            }

            // 外部CLI入力は欠落と重複を拒否し、曖昧な起動を許可しない
            // Reject missing and duplicate external CLI values so the boot is unambiguous
            if (!TryReadRequiredOption(args, ServerDirectoryOption, out var serverDirectory, out error)) return false;
            if (!TryReadRequiredOption(args, WorldDirectoryOption, out var worldDirectory, out error)) return false;
            if (!TryReadRequiredOption(args, ResultDirectoryOption, out var resultDirectory, out error)) return false;
            if (!TryReadRequiredOption(args, SeedOption, out var seedText, out error)) return false;

            if (!int.TryParse(seedText, out var seed))
            {
                error = $"{SeedOption} must be an integer";
                return false;
            }

            settings = new StandaloneTerrainQaSettings(serverDirectory, worldDirectory, resultDirectory, seed);
            error = string.Empty;
            return true;
        }

        public InitializeProprieties CreateInitializeProprieties()
        {
            // QA固有値を既存サーバー設定へ集約し、初期化パイプラインを迂回しない
            // Gather QA values into the existing server settings without bypassing the initialization pipeline
            var serverSettings = new StartServerSettings
            {
                ServerDataDirectory = _serverDirectory,
                WorldDirectory = _worldDirectory,
                MapMode = WorldProvisioner.GeneratedMapMode,
                Seed = _seed,
                AutoSave = false,
            };

            var proprieties = InitializeProprieties.CreateLocalServer(null);
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(serverSettings);
            return proprieties;
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
