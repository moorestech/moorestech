using Client.Starter;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Playtest
{
    internal static class PlaytestWorldBootSession
    {
        private const string PendingKey = "Playtest_WorldBootPending";
        private const string ServerDirectoryKey = "Playtest_WorldBootServerDirectory";
        private const string WorldDirectoryKey = "Playtest_WorldBootWorldDirectory";
        private const string MapModeKey = "Playtest_WorldBootMapMode";
        private const string SeedKey = "Playtest_WorldBootSeed";

        public static void Save(string serverDirectory, string worldDirectory, string mapMode, int seed)
        {
            // domain reload後にsceneLoadedから復元できるよう全起動値をSessionStateへ保存する
            // Store every boot value in SessionState so sceneLoaded can restore it after domain reload
            SessionState.SetString(ServerDirectoryKey, serverDirectory);
            SessionState.SetString(WorldDirectoryKey, worldDirectory);
            SessionState.SetString(MapModeKey, mapMode);
            SessionState.SetInt(SeedKey, seed);
            SessionState.SetBool(PendingKey, true);
        }

        public static bool TryCreateInitializeProprieties(out InitializeProprieties proprieties)
        {
            if (!SessionState.GetBool(PendingKey, false))
            {
                proprieties = null;
                return false;
            }

            // 起動値を正式なCLI設定へ戻し、通常のローカルサーバー初期化へ渡す
            // Restore boot values into the official CLI settings for normal local-server initialization
            var settings = new StartServerSettings
            {
                ServerDataDirectory = SessionState.GetString(ServerDirectoryKey, string.Empty),
                WorldDirectory = SessionState.GetString(WorldDirectoryKey, string.Empty),
                MapMode = SessionState.GetString(MapModeKey, string.Empty),
                Seed = SessionState.GetInt(SeedKey, 0),
                AutoSave = false,
            };

            proprieties = InitializeProprieties.CreateLocalServer(null);
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(settings);
            return true;
        }

        public static bool IsPending()
        {
            return SessionState.GetBool(PendingKey, false);
        }

        public static void Clear()
        {
            // 通常再生へ設定が漏れないようpendingと値を同時に初期化する
            // Reset the pending flag and values together so settings never leak into normal play
            SessionState.SetBool(PendingKey, false);
            SessionState.SetString(ServerDirectoryKey, string.Empty);
            SessionState.SetString(WorldDirectoryKey, string.Empty);
            SessionState.SetString(MapModeKey, string.Empty);
            SessionState.SetInt(SeedKey, 0);
        }
    }
}
