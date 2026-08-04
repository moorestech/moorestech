#if UNITY_EDITOR
using System;
using System.IO;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Starter.Editor
{
    public static class SkipSaveLoadPlayModeSettings
    {
        public const string SessionStateKey = "moorestech_SkipSaveLoadPlayMode";

        public static void ApplyIfNeeded(InitializeProprieties proprieties)
        {
            if (!SessionState.GetBool(SessionStateKey, false)) return;

            // 存在しない一時worldと保存無効化で既存セーブの読み書きを防ぐ
            // Prevent existing save reads and writes with a missing temporary world and disabled saving
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            settings.WorldDirectory = Path.Combine(Path.GetTempPath(), $"no_save_play_mode_{Guid.NewGuid()}");
            settings.AutoSave = false;
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(settings);
        }
    }
}
#endif
