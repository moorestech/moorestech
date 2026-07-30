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

            // 専用の一時ワールドと自動保存無効化でテスト再生を隔離する
            // Isolate test play with a dedicated temporary world and disabled auto-save
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            settings.WorldDirectory = Path.Combine(Path.GetTempPath(), $"no_save_play_mode_{Guid.NewGuid()}");
            settings.AutoSave = false;
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(settings);
        }
    }
}
#endif
