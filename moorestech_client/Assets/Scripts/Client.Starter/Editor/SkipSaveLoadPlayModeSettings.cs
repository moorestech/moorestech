#if UNITY_EDITOR
using System;
using System.IO;
using Client.Game.InGame.BugReport.Recording;
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

            // 録画リングもCaptureRingと同じ役割で無効化する。このPlayMode経路がScreenCapture等を奪い合わないため
            // Disables the recording ring in the same role as CaptureRing, so this PlayMode path never contends over ScreenCapture etc.
            BugReportRecordingSettings.SetEnabled(false);

            // 存在しない一時worldと保存無効化で既存セーブの読み書きを防ぐ
            // Prevent existing save reads and writes with a missing temporary world and disabled saving
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            settings.WorldDirectory = Path.Combine(Path.GetTempPath(), $"no_save_play_mode_{Guid.NewGuid()}");
            settings.AutoSave = false;
            settings.CaptureRing = false;
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(settings);
        }
    }
}
#endif
