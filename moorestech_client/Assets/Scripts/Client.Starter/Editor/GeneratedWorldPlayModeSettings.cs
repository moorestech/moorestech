#if UNITY_EDITOR
using Client.DebugSystem.Environment;
using Common.Debug;
using Game.MapGeneration.Provisioning;
using Game.Paths;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Starter.Editor
{
    public static class GeneratedWorldPlayModeSettings
    {
        public const string SessionStateKey = "moorestech_GeneratedWorldPlayMode";
        private const string WorldDirectoryName = "world_generated";

        // DebugEnvironmentControllerと重複定義（各所で共有されているprivate constの既存流儀）
        // Duplicated from DebugEnvironmentController (existing convention of a shared private const per file)
        private const string DebugEnvironmentTypeKey = "DebugEnvironmentTypeKey";
        private const string PreviousDebugEnvironmentTypeSessionKey = "moorestech_GeneratedWorldPlayMode_PreviousDebugEnvironmentType";
        private const string HasPreviousDebugEnvironmentTypeSessionKey = "moorestech_GeneratedWorldPlayMode_HasPreviousDebugEnvironmentType";

        // 起動ボタンと削除メニューが共有する生成ワールドの保存先
        // Generated world save path shared by the play button and the delete menu
        public static string WorldDirectoryPath => GameSystemPaths.GetSaveFilePath(WorldDirectoryName);

        public static void ApplyIfNeeded(InitializeProprieties proprieties)
        {
            if (!SessionState.GetBool(SessionStateKey, false)) return;

            // 専用ワールドと自動生成モードだけを上書きする（セーブは通常どおり有効のまま）
            // Override only the dedicated world and generated map mode (saving stays enabled)
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            settings.WorldDirectory = WorldDirectoryPath;
            settings.MapMode = WorldProvisioner.GeneratedMapMode;
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(settings);
        }

        public static void ApplyDebugEnvironmentOverride()
        {
            // 未退避の時だけ現在値を退避する。EnterPlaymode失敗等でRestoreが未実行のまま再クリックされても、既にRuntimeへ汚染された値を「元値」として誤って再退避しない
            // Save the current value only if not already saved, so a retry after a failed EnterPlaymode (Restore never ran) does not re-save an already-corrupted Runtime value as the "original"
            if (!SessionState.GetBool(HasPreviousDebugEnvironmentTypeSessionKey, false))
            {
                var previousValue = DebugParameters.GetValueOrDefaultInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Debug);
                SessionState.SetInt(PreviousDebugEnvironmentTypeSessionKey, previousValue);
                SessionState.SetBool(HasPreviousDebugEnvironmentTypeSessionKey, true);
            }

            // Runtimeへの上書き自体は冪等なので毎回実行してよい
            // Overwriting to Runtime itself is idempotent, so it always runs
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Runtime);
        }

        public static void RestoreDebugEnvironmentIfNeeded()
        {
            // 自分が退避した時だけ復元する（通常再生の終了時に手動選択値を上書きしないため）
            // Restore only when this feature saved a value (avoid overwriting a manual choice on normal play's end)
            if (!SessionState.GetBool(HasPreviousDebugEnvironmentTypeSessionKey, false)) return;

            var previousValue = SessionState.GetInt(PreviousDebugEnvironmentTypeSessionKey, (int)DebugEnvironmentType.Debug);
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, previousValue);
            SessionState.SetBool(HasPreviousDebugEnvironmentTypeSessionKey, false);
        }
    }
}
#endif
